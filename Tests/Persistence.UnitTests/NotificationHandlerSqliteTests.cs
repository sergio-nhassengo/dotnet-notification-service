using Application.Features.Notifications.Queries.GetNextDueEmailDelivery;
using Application.Features.Notifications.Queries.GetNextPendingOutboxMessage;
using Domain.Entities;
using Domain.Enums;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Persistence.UnitTests;

public sealed class NotificationHandlerSqliteTests
{
    [Fact]
    public async Task GetNextPendingOutboxMessage_returns_oldest_due_pending_message_without_mutating_state()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var now = new DateTimeOffset(2026, 9, 1, 12, 0, 0, TimeSpan.Zero);
        await using var context = CreateContext(connection, now);
        await context.Database.EnsureCreatedAsync();

        var first = CreateOutbox(now.AddMinutes(-3), now.AddMinutes(-1));
        var second = CreateOutbox(now.AddMinutes(-2), now);
        var third = CreateOutbox(now.AddMinutes(-1), now.AddMinutes(-1));
        var future = CreateOutbox(now.AddMinutes(-4), now.AddMinutes(1));
        var processed = CreateOutbox(now.AddMinutes(-5), now.AddMinutes(-1));
        processed.ProcessedAt = now.AddMinutes(-1);
        context.OutboxMessages.AddRange(first, second, third, future, processed);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var handler = new GetNextPendingOutboxMessageQueryHandler(context);
        var selected = await handler.Handle(new GetNextPendingOutboxMessageQuery(now), CancellationToken.None);

        Assert.NotNull(selected);
        Assert.Equal(first.Id, selected.Id);
        Assert.Empty(context.ChangeTracker.Entries());

        var persisted = await context.OutboxMessages.OrderBy(x => x.OccurredAt).ToListAsync();
        Assert.Equal(5, persisted.Count);
        Assert.Equal(4, persisted.Count(x => x.ProcessedAt is null));
        Assert.All(persisted, x => Assert.Equal(0, x.AttemptCount));
    }

    [Fact]
    public async Task GetNextDueEmailDelivery_returns_oldest_due_notification_using_fifo_without_mutating_state()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var now = new DateTimeOffset(2026, 9, 1, 12, 0, 0, TimeSpan.Zero);
        await using var context = CreateContext(connection, now);
        await context.Database.EnsureCreatedAsync();

        var firstQueued = CreateNotification(now.AddMinutes(-2), now.AddMinutes(-2));
        var secondQueued = CreateNotification(now.AddMinutes(-1), now.AddMinutes(-1));
        var retryScheduled = CreateNotification(now.AddMinutes(-3), now.AddMinutes(-3));
        retryScheduled.RecordAttempt(now.AddMinutes(-4), now.AddMinutes(-4));
        retryScheduled.ScheduleRetry("Transient", "Retry", now.AddMinutes(-3), now.AddMinutes(-4));
        var future = CreateNotification(now.AddMinutes(1), now);
        var pending = EmailNotification.Create(
            Guid.NewGuid(), Guid.NewGuid().ToString(), Guid.NewGuid().ToString(),
            "sender@example.com", null,
            "payment-confirmed", "{}", null, now);
        context.EmailNotifications.AddRange(firstQueued, secondQueued, retryScheduled, future, pending);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var handler = new GetNextDueEmailDeliveryQueryHandler(context);
        var selected = await handler.Handle(new GetNextDueEmailDeliveryQuery(now), CancellationToken.None);

        Assert.NotNull(selected);
        Assert.Equal(retryScheduled.Id, selected.Id);
        Assert.Empty(context.ChangeTracker.Entries());

        var persisted = await context.EmailNotifications.ToListAsync();
        Assert.Equal(5, persisted.Count);
        Assert.Equal(4, persisted.Count(x => x.Status == NotificationStatus.RetryScheduled));
        Assert.Single(persisted, x => x.Status == NotificationStatus.Pending);
        Assert.All(persisted.Where(x => x.Status == NotificationStatus.Queued), x => Assert.Equal(0, x.AttemptCount));
    }

    [Fact]
    public async Task GetNextDueEmailDelivery_uses_created_at_when_due_times_are_equal()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var now = new DateTimeOffset(2026, 9, 1, 12, 0, 0, TimeSpan.Zero);
        await using var context = CreateContext(connection, now);
        await context.Database.EnsureCreatedAsync();

        var dueAt = now.AddMinutes(-1);
        var newer = CreateNotification(dueAt, now);
        var older = CreateNotification(dueAt, now.AddMinutes(-1));
        context.EmailNotifications.AddRange(newer, older);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var handler = new GetNextDueEmailDeliveryQueryHandler(context);
        var selected = await handler.Handle(new GetNextDueEmailDeliveryQuery(now), CancellationToken.None);

        Assert.NotNull(selected);
        Assert.Equal(older.Id, selected.Id);
        Assert.Empty(context.ChangeTracker.Entries());
    }

    [Fact]
    public async Task GetNextPendingOutboxMessage_returns_null_when_no_pending_message_is_due()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var now = new DateTimeOffset(2026, 9, 1, 12, 0, 0, TimeSpan.Zero);
        await using var context = CreateContext(connection, now);
        await context.Database.EnsureCreatedAsync();

        var future = CreateOutbox(now, now.AddMinutes(1));
        var processed = CreateOutbox(now.AddMinutes(-1), now.AddMinutes(-1));
        processed.ProcessedAt = now;
        context.OutboxMessages.AddRange(future, processed);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var handler = new GetNextPendingOutboxMessageQueryHandler(context);
        var selected = await handler.Handle(new GetNextPendingOutboxMessageQuery(now), CancellationToken.None);

        Assert.Null(selected);
        Assert.Empty(context.ChangeTracker.Entries());
    }

    [Fact]
    public async Task GetNextDueEmailDelivery_returns_null_when_no_queued_or_retry_scheduled_notification_is_due()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var now = new DateTimeOffset(2026, 9, 1, 12, 0, 0, TimeSpan.Zero);
        await using var context = CreateContext(connection, now);
        await context.Database.EnsureCreatedAsync();

        var future = CreateNotification(now.AddMinutes(1), now);
        var pending = EmailNotification.Create(
            Guid.NewGuid(), Guid.NewGuid().ToString(), Guid.NewGuid().ToString(),
            "sender@example.com", null,
            "payment-confirmed", "{}", null, now);
        context.EmailNotifications.AddRange(future, pending);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var handler = new GetNextDueEmailDeliveryQueryHandler(context);
        var selected = await handler.Handle(new GetNextDueEmailDeliveryQuery(now), CancellationToken.None);

        Assert.Null(selected);
        Assert.Empty(context.ChangeTracker.Entries());
    }

    private static ApplicationDbContext CreateContext(SqliteConnection connection, DateTimeOffset now)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options;
        return new ApplicationDbContext(options);
    }

    private static OutboxMessage CreateOutbox(DateTimeOffset occurredAt, DateTimeOffset nextAttemptAt) => new()
    {
        Id = Guid.NewGuid(),
        MessageKey = Guid.NewGuid(),
        Topic = "test-topic",
        Payload = "{}",
        OccurredAt = occurredAt,
        NextAttemptAt = nextAttemptAt,
        CreatedAt = occurredAt
    };

    private static EmailNotification CreateNotification(DateTimeOffset dueAt, DateTimeOffset now)
    {
        var notification = EmailNotification.Create(
            Guid.NewGuid(), Guid.NewGuid().ToString(), Guid.NewGuid().ToString(),
            "sender@example.com", null,
            "payment-confirmed", "{}", null, now);
        notification.MarkQueued(now);
        notification.ScheduleRetry("Transient", "Retry", dueAt, now);
        return notification;
    }
}
