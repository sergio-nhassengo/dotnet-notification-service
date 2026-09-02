using Application.Common.Security;
using Domain.Entities;
using Domain.Enums;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace Persistence.UnitTests;

public sealed class NotificationStoreSqliteTests
{
    [Fact]
    public async Task ClaimOutboxAsync_filters_and_orders_DateTimeOffset_values_in_Sqlite()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var now = new DateTimeOffset(2026, 9, 1, 12, 0, 0, TimeSpan.Zero);
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connection)
            .Options;
        var currentUser = Substitute.For<ICurrentUserService>();
        await using var context = new ApplicationDbContext(options, new FakeDateTime(now), currentUser);
        await context.Database.EnsureCreatedAsync();

        context.OutboxMessages.AddRange(
            CreateOutbox(now.AddMinutes(-2), now.AddMinutes(-1)),
            CreateOutbox(now.AddMinutes(-1), now.AddMinutes(1)));
        await context.SaveChangesAsync();

        var store = new NotificationStore(context);
        var claimed = await store.ClaimOutboxAsync("test-worker", 10, now, TimeSpan.FromMinutes(1), CancellationToken.None);

        var message = Assert.Single(claimed);
        Assert.Equal(now.AddMinutes(-2), message.OccurredAt);
        Assert.Equal("test-worker", message.LeaseOwner);
    }

    [Fact]
    public async Task ClaimDeliveriesAsync_filters_nullable_DateTimeOffset_values_in_Sqlite()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var now = new DateTimeOffset(2026, 9, 1, 12, 0, 0, TimeSpan.Zero);
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connection)
            .Options;
        var currentUser = Substitute.For<ICurrentUserService>();
        await using var context = new ApplicationDbContext(options, new FakeDateTime(now), currentUser);
        await context.Database.EnsureCreatedAsync();

        var due = CreateNotification(now.AddMinutes(-1), now);
        var future = CreateNotification(now.AddMinutes(1), now);
        context.EmailNotifications.AddRange(due, future);
        await context.SaveChangesAsync();

        var store = new NotificationStore(context);
        var claimed = await store.ClaimDeliveriesAsync("test-worker", 10, now, TimeSpan.FromMinutes(1), CancellationToken.None);

        var notification = Assert.Single(claimed);
        Assert.Equal(due.Id, notification.Id);
        Assert.Equal("test-worker", notification.LeaseOwner);
    }

    private static OutboxMessage CreateOutbox(DateTimeOffset occurredAt, DateTimeOffset nextAttemptAt) => new()
    {
        Id = Guid.NewGuid(),
        EventType = "test",
        SchemaVersion = 1,
        MessageKey = Guid.NewGuid(),
        Topic = "test-topic",
        Payload = "{}",
        OccurredAt = occurredAt,
        NextAttemptAt = nextAttemptAt,
        CreatedAt = occurredAt
    };

    private static EmailNotification CreateNotification(DateTimeOffset scheduledAt, DateTimeOffset now)
    {
        var notification = EmailNotification.Create(
            Guid.NewGuid(), Guid.NewGuid().ToString(), Guid.NewGuid().ToString(),
            "recipient@example.com", null, "sender@example.com", null, null,
            "payment-confirmed", 1, "{}", null, NotificationPriority.Normal,
            NotificationSource.RestApi, now, scheduledAt, now);
        notification.MarkQueued(now);
        return notification;
    }
}
