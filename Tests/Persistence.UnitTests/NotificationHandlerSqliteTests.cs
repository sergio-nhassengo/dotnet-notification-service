using Application.Common.Security;
using Application.Features.Notifications.Queries.Delivery;
using Application.Features.Notifications.Queries.Outbox;
using Domain.Entities;
using Domain.Enums;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace Persistence.UnitTests;

public sealed class NotificationHandlerSqliteTests
{
    [Fact]
    public async Task GetPendingOutboxBatch_filters_orders_and_enforces_batch_size_without_mutating_state()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var now = new DateTimeOffset(2026, 9, 1, 12, 0, 0, TimeSpan.Zero);
        await using var context = CreateContext(connection, now);
        await context.Database.EnsureCreatedAsync();

        var first = CreateOutbox(now.AddMinutes(-3), now.AddMinutes(-1));
        var second = CreateOutbox(now.AddMinutes(-2), now);
        var outsideBatch = CreateOutbox(now.AddMinutes(-1), now.AddMinutes(-1));
        var future = CreateOutbox(now.AddMinutes(-4), now.AddMinutes(1));
        var processed = CreateOutbox(now.AddMinutes(-5), now.AddMinutes(-1));
        processed.ProcessedAt = now.AddMinutes(-1);
        context.OutboxMessages.AddRange(first, second, outsideBatch, future, processed);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var handler = new GetPendingOutboxBatchQueryHandler(context);
        var selected = await handler.Handle(new GetPendingOutboxBatchQuery(2, now), CancellationToken.None);

        Assert.Equal([first.Id, second.Id], selected.Select(x => x.Id));
        Assert.Empty(context.ChangeTracker.Entries());

        var persisted = await context.OutboxMessages.OrderBy(x => x.OccurredAt).ToListAsync();
        Assert.Equal(5, persisted.Count);
        Assert.Equal(4, persisted.Count(x => x.ProcessedAt is null));
        Assert.All(persisted, x => Assert.Equal(0, x.AttemptCount));
    }

    [Fact]
    public async Task GetDueEmailDeliveryBatch_filters_orders_and_enforces_batch_size_without_mutating_state()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var now = new DateTimeOffset(2026, 9, 1, 12, 0, 0, TimeSpan.Zero);
        await using var context = CreateContext(connection, now);
        await context.Database.EnsureCreatedAsync();

        var highFirst = CreateNotification(now.AddMinutes(-2), now, NotificationPriority.High);
        var highSecond = CreateNotification(now.AddMinutes(-1), now, NotificationPriority.High);
        var retryOutsideBatch = CreateNotification(now.AddMinutes(-3), now, NotificationPriority.Normal);
        retryOutsideBatch.StartAttempt(now.AddMinutes(-4));
        retryOutsideBatch.ScheduleRetry("Transient", "Retry", now.AddMinutes(-3), now.AddMinutes(-4));
        var future = CreateNotification(now.AddMinutes(1), now, NotificationPriority.High);
        var pending = EmailNotification.Create(
            Guid.NewGuid(), Guid.NewGuid().ToString(), Guid.NewGuid().ToString(),
            "recipient@example.com", null, "sender@example.com", null, null,
            "payment-confirmed", 1, "{}", null, NotificationPriority.High,
            NotificationSource.RestApi, now, now.AddMinutes(-5), now);
        context.EmailNotifications.AddRange(highFirst, highSecond, retryOutsideBatch, future, pending);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var handler = new GetDueEmailDeliveryBatchQueryHandler(context);
        var selected = await handler.Handle(new GetDueEmailDeliveryBatchQuery(2, now), CancellationToken.None);

        Assert.Equal([highFirst.Id, highSecond.Id], selected.Select(x => x.Id));
        Assert.Empty(context.ChangeTracker.Entries());

        var persisted = await context.EmailNotifications.ToListAsync();
        Assert.Equal(5, persisted.Count);
        Assert.Equal(3, persisted.Count(x => x.Status == NotificationStatus.Queued && x.Priority == NotificationPriority.High));
        Assert.Single(persisted, x => x.Status == NotificationStatus.RetryScheduled);
        Assert.Single(persisted, x => x.Status == NotificationStatus.Pending);
        Assert.All(persisted.Where(x => x.Status == NotificationStatus.Queued), x => Assert.Equal(0, x.AttemptCount));
    }

    private static ApplicationDbContext CreateContext(SqliteConnection connection, DateTimeOffset now)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options;
        return new ApplicationDbContext(options, new FakeDateTime(now), Substitute.For<ICurrentUserService>());
    }

    private static OutboxMessage CreateOutbox(DateTimeOffset occurredAt, DateTimeOffset nextAttemptAt) => new()
    {
        Id = Guid.NewGuid(), EventType = "test", SchemaVersion = 1, MessageKey = Guid.NewGuid(),
        Topic = "test-topic", Payload = "{}", OccurredAt = occurredAt,
        NextAttemptAt = nextAttemptAt, CreatedAt = occurredAt
    };

    private static EmailNotification CreateNotification(
        DateTimeOffset scheduledAt, DateTimeOffset now, NotificationPriority priority)
    {
        var notification = EmailNotification.Create(
            Guid.NewGuid(), Guid.NewGuid().ToString(), Guid.NewGuid().ToString(),
            "recipient@example.com", null, "sender@example.com", null, null,
            "payment-confirmed", 1, "{}", null, priority,
            NotificationSource.RestApi, now, scheduledAt, now);
        notification.MarkQueued(now);
        return notification;
    }
}
