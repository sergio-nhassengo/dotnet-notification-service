using Domain.Entities;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Microsoft.EntityFrameworkCore;

namespace Persistence.UnitTests;

public sealed class NotificationMigrationTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 10, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Fresh_sqlite_migrates_and_supports_current_model_and_unique_indexes()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = Context(connection);
        await db.Database.MigrateAsync();
        Assert.Empty(await db.Database.GetPendingMigrationsAsync());
        var n = Notification();
        db.EmailNotifications.Add(n);
        db.OutboxMessages.Add(new OutboxMessage { Id = Guid.NewGuid(), MessageKey = n.MessageId,
            Topic = "test", Payload = "{}", CreatedAt = Now, OccurredAt = Now, NextAttemptAt = Now });
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        Assert.Equal(Now, (await db.EmailNotifications.SingleAsync()).CreatedAt);
        Assert.Equal(Now, (await db.OutboxMessages.SingleAsync()).NextAttemptAt);
        var duplicate = Notification();
        db.Entry(duplicate).Property(x => x.MessageId).CurrentValue = n.MessageId;
        db.EmailNotifications.Add(duplicate);
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        db.ChangeTracker.Clear();
        duplicate = Notification();
        db.Entry(duplicate).Property(x => x.IdempotencyKey).CurrentValue = n.IdempotencyKey;
        db.EmailNotifications.Add(duplicate);
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task Existing_development_database_without_history_is_upgraded_without_losing_durable_records()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = Context(connection);
        var schema = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "Fixtures", "LegacyDevelopmentSqlite.sql"));
        await db.Database.ExecuteSqlRawAsync(schema);
        var n = Notification();
        var converter = new DateTimeOffsetToBinaryConverter().ConvertToProviderExpression.Compile();
        var timestamp = converter(Now);
        var later = converter(Now.AddDays(1));
        await db.Database.ExecuteSqlInterpolatedAsync($$"""
            INSERT INTO EmailNotifications
                (Id, MessageId, ApplicationId, IdempotencyKey, RequestHash, CorrelationId, RecipientEmail,
                SenderEmail, TemplateId, TemplateVariables, Status, Source, RequestedAt, ScheduledAt,
                AttemptCount, NextAttemptAt, CreatedAt, UpdatedAt)
            VALUES ({{n.Id}}, {{n.MessageId}}, 'otc-api', {{n.IdempotencyKey}}, 'hash', 'c', 'a@example.com',
                'b@example.com', 'payment-confirmed', '{}', 0, 0, {{timestamp}}, {{later}}, 1, {{later}}, {{timestamp}}, {{timestamp}});
            """);
        await db.Database.ExecuteSqlInterpolatedAsync($$"""
            INSERT INTO NotificationDeliveryAttempts
                (Id, NotificationId, AttemptNumber, Provider, StartedAt, Outcome, ErrorCategory)
            VALUES ({{Guid.NewGuid()}}, {{n.Id}}, 1, 'Fake', {{timestamp}}, 0, 0);
            INSERT INTO OutboxMessages
                (Id, MessageKey, Topic, Payload, Headers, CreatedAt, OccurredAt, AttemptCount, NextAttemptAt, EventType, SchemaVersion)
            VALUES ({{Guid.NewGuid()}}, {{n.MessageId}}, 'test', '{"durable":true}', '{}', {{timestamp}}, {{timestamp}}, 0, {{timestamp}}, 'email.requested', 1);
            INSERT INTO NotificationAttachments (Id, ApplicationId, FileName, ContentType, Content, CreatedAt)
            VALUES ({{Guid.NewGuid()}}, 'otc-api', 'a.txt', 'text/plain', X'0102', {{timestamp}});
            """);
        await db.Database.MigrateAsync();
        await db.Database.MigrateAsync(); // Restart is safe.
        var stored = await db.EmailNotifications.SingleAsync();
        Assert.Equal(n.Id, stored.Id);
        Assert.Equal(n.MessageId, stored.MessageId);
        Assert.Equal("hash", stored.RequestHash);
        var content = System.Text.Json.JsonSerializer.Deserialize<Application.Notifications.Models.EmailContent>(stored.ContentJson!);
        Assert.Equal("a@example.com", Assert.Single(content!.To).Email);
        Assert.Equal(Now, stored.LastAttemptAt);
        Assert.Equal(stored.CreatedAt, stored.NextAttemptAt);
        Assert.Equal("{\"durable\":true}", (await db.OutboxMessages.SingleAsync()).Payload);
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name IN ('InboxMessages', 'NotificationDeliveryAttempts', 'NotificationReplays', 'Users', 'Roles', 'NotificationAttachments')";
        Assert.Equal(0L, await command.ExecuteScalarAsync());
        command.CommandText = "SELECT COUNT(*) FROM pragma_table_info('EmailNotifications') WHERE name IN ('RecipientEmail', 'RecipientName', 'BusinessReference')";
        Assert.Equal(0L, await command.ExecuteScalarAsync());
        Assert.Empty(await db.Database.GetPendingMigrationsAsync());
    }

    [Fact]
    public void Sql_server_upgrade_script_preserves_rows_and_adds_a_distinct_timestamp_column()
    {
        using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer("Server=localhost;Database=script-only;Integrated Security=true;TrustServerCertificate=true").Options);
        var script = db.GetService<IMigrator>().GenerateScript("20260909120547_RemoveTemplateVersion");
        Assert.Contains("ADD [LastAttemptAt]", script);
        Assert.DoesNotContain("sp_rename", script);
        Assert.DoesNotContain("DROP TABLE [EmailNotifications]", script);
        Assert.DoesNotContain("DROP TABLE [OutboxMessages]", script);
        Assert.False(db.Database.HasPendingModelChanges());
    }

    [Fact]
    public void Sql_server_script_drops_users_before_roles()
    {
        using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer("Server=localhost;Database=script-only;Integrated Security=true;TrustServerCertificate=true").Options);
        var script = db.GetService<IMigrator>().GenerateScript("20260917090000_RemoveRedundantNotificationFields");
        var users = script.IndexOf("DROP TABLE [Users]", StringComparison.Ordinal);
        Assert.True(users >= 0);
        Assert.True(script.IndexOf("DROP TABLE [Roles]", StringComparison.Ordinal) > users);
    }

    private static ApplicationDbContext Context(SqliteConnection connection) => new(
        new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection)
            .ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning)).Options);

    private static EmailNotification Notification()
    {
        var n = EmailNotification.Create(Guid.NewGuid(), "c", Guid.NewGuid().ToString(),
            "b@example.com", null, "payment-confirmed", "{}", null, Now);
        n.SetIntegrationContext("otc-api", "hash", null);
        return n;
    }
}
