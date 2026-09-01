using Application.Notifications.Interfaces;
using Application.Notifications.Models;
using Domain.Entities;
using Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Persistence;

public sealed class NotificationStore(ApplicationDbContext db) : INotificationStore
{
    public async Task<EmailNotification> AcceptRestAsync(EmailNotification notification, OutboxMessage outbox, CancellationToken ct)
    {
        var existing = await db.EmailNotifications.FirstOrDefaultAsync(x => x.IdempotencyKey == notification.IdempotencyKey, ct);
        if (existing is not null) return existing;
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        try
        {
            db.EmailNotifications.Add(notification); db.OutboxMessages.Add(outbox);
            await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct); return notification;
        }
        catch (DbUpdateException)
        {
            await transaction.RollbackAsync(ct); db.ChangeTracker.Clear();
            return await db.EmailNotifications.SingleAsync(x => x.IdempotencyKey == notification.IdempotencyKey, ct);
        }
    }

    public async Task<bool> AcceptKafkaAsync(EmailNotification? notification, InboxMessage inbox, OutboxMessage? invalidDlq, CancellationToken ct)
    {
        if (await db.InboxMessages.AnyAsync(x => x.ConsumerName == inbox.ConsumerName && x.MessageId == inbox.MessageId, ct)) return false;
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        try
        {
            inbox.ProcessedAt = DateTimeOffset.UtcNow; db.InboxMessages.Add(inbox);
            if (notification is not null)
            {
                var existing = await db.EmailNotifications.FirstOrDefaultAsync(x => x.MessageId == notification.MessageId || x.IdempotencyKey == notification.IdempotencyKey, ct);
                if (existing is null)
                {
                    notification.MarkQueued(inbox.ReceivedAt);
                    db.EmailNotifications.Add(notification);
                }
                else if (existing.Status == NotificationStatus.Pending)
                {
                    existing.MarkQueued(inbox.ReceivedAt);
                }
            }
            if (invalidDlq is not null) db.OutboxMessages.Add(invalidDlq);
            await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct); return true;
        }
        catch (DbUpdateException)
        {
            await transaction.RollbackAsync(ct); db.ChangeTracker.Clear(); return false;
        }
    }

    public Task<EmailNotification?> FindAsync(Guid id, CancellationToken ct) =>
        db.EmailNotifications.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);

    public async Task<IReadOnlyList<DeliveryAttempt>> GetAttemptsAsync(Guid id, CancellationToken ct) =>
        await db.DeliveryAttempts.AsNoTracking().Where(x => x.NotificationId == id).OrderBy(x => x.AttemptNumber).ToListAsync(ct);

    public async Task<bool> ReplayAsync(Guid id, string actor, DateTimeOffset now, CancellationToken ct)
    {
        var entity = await db.EmailNotifications.SingleOrDefaultAsync(x => x.Id == id, ct);
        if (entity is null || entity.Status is not (NotificationStatus.DeadLettered or NotificationStatus.PermanentlyFailed)) return false;
        db.NotificationReplays.Add(new NotificationReplay
        {
            Id = Guid.NewGuid(),
            NotificationId = id,
            RequestedBy = actor[..Math.Min(actor.Length, 200)],
            RequestedAt = now,
            PreviousAttemptCount = entity.AttemptCount
        });
        entity.Replay(now);
        await db.SaveChangesAsync(ct); return true;
    }

    public Task<IReadOnlyList<OutboxMessage>> ClaimOutboxAsync(string owner, int batchSize, DateTimeOffset now, TimeSpan lease, CancellationToken ct) =>
        ClaimOutboxCoreAsync(owner, batchSize, now, lease, ct);
    private async Task<IReadOnlyList<OutboxMessage>> ClaimOutboxCoreAsync(string owner, int batchSize, DateTimeOffset now, TimeSpan lease, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
        var rows = await db.OutboxMessages.Where(x => x.ProcessedAt == null && x.NextAttemptAt <= now && (x.LeaseExpiresAt == null || x.LeaseExpiresAt < now))
            .OrderBy(x => x.OccurredAt).Take(batchSize).ToListAsync(ct);
        foreach (var row in rows) { row.LeaseOwner = owner; row.LeaseExpiresAt = now.Add(lease); }
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return rows;
    }

    public async Task MarkOutboxProcessedAsync(Guid id, DateTimeOffset now, CancellationToken ct)
    {
        var row = await db.OutboxMessages.SingleAsync(x => x.Id == id, ct); row.ProcessedAt = now; row.LeaseOwner = null; row.LeaseExpiresAt = null; row.LastError = null; await db.SaveChangesAsync(ct);
    }
    public async Task MarkOutboxFailedAsync(Guid id, string error, DateTimeOffset next, CancellationToken ct)
    {
        var row = await db.OutboxMessages.SingleAsync(x => x.Id == id, ct); row.AttemptCount++; row.LastError = error;
        row.NextAttemptAt = next; row.LeaseOwner = null; row.LeaseExpiresAt = null; await db.SaveChangesAsync(ct);
    }

    public Task<IReadOnlyList<EmailNotification>> ClaimDeliveriesAsync(string owner, int batchSize, DateTimeOffset now, TimeSpan lease, CancellationToken ct) =>
        ClaimDeliveriesCoreAsync(owner, batchSize, now, lease, ct);
    private async Task<IReadOnlyList<EmailNotification>> ClaimDeliveriesCoreAsync(string owner, int batchSize, DateTimeOffset now, TimeSpan lease, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
        var states = new[] { NotificationStatus.Queued, NotificationStatus.RetryScheduled };
        var rows = await db.EmailNotifications.Where(x => states.Contains(x.Status) && x.NextAttemptAt <= now && (x.LeaseExpiresAt == null || x.LeaseExpiresAt < now))
            .OrderByDescending(x => x.Priority).ThenBy(x => x.NextAttemptAt).Take(batchSize).ToListAsync(ct);
        foreach (var row in rows) row.Claim(owner, now.Add(lease));
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return rows;
    }

    public async Task RecordDeliveryResultAsync(Guid id, string provider, DateTimeOffset startedAt, EmailProviderResult result,
        DateTimeOffset now, DateTimeOffset? next, OutboxMessage? dlq, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var entity = await db.EmailNotifications.SingleAsync(x => x.Id == id, ct); entity.StartAttempt(startedAt);
        var outcome = result.IsSuccess ? DeliveryOutcome.Succeeded : result.FailureCategory switch
        {
            EmailFailureCategory.Transient => DeliveryOutcome.TransientFailure,
            EmailFailureCategory.RateLimited => DeliveryOutcome.RateLimited,
            EmailFailureCategory.Configuration => DeliveryOutcome.ConfigurationFailure,
            _ => DeliveryOutcome.PermanentFailure
        };
        db.DeliveryAttempts.Add(new DeliveryAttempt
        {
            Id = Guid.NewGuid(),
            NotificationId = id,
            AttemptNumber = entity.AttemptCount,
            Provider = provider,
            StartedAt = startedAt,
            CompletedAt = now,
            Outcome = outcome,
            ProviderStatusCode = result.StatusCode,
            ProviderMessageId = result.ProviderMessageId,
            ErrorCategory = result.FailureCategory,
            ErrorCode = result.ErrorCode,
            SafeErrorMessage = result.SafeErrorMessage,
            NextAttemptAt = next
        });
        if (result.IsSuccess) entity.MarkSent(result.ProviderMessageId, now);
        else if (next is not null) entity.ScheduleRetry(result.ErrorCode ?? "TransientFailure", result.SafeErrorMessage ?? "Delivery failed.", next.Value, now);
        else
        {
            entity.MarkPermanentlyFailed(result.ErrorCode ?? "PermanentFailure", result.SafeErrorMessage ?? "Delivery failed.", now);
            if (dlq is not null) { db.OutboxMessages.Add(dlq); entity.MarkDeadLettered(now); }
        }
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
    }

    public async Task<int> CleanupOutboxAsync(DateTimeOffset before, CancellationToken ct) =>
        await db.OutboxMessages.Where(x => x.ProcessedAt < before).ExecuteDeleteAsync(ct);
    public async Task<(long Pending, double OldestAgeSeconds)> GetOutboxStatsAsync(DateTimeOffset now, CancellationToken ct)
    {
        var q = db.OutboxMessages.Where(x => x.ProcessedAt == null); var count = await q.LongCountAsync(ct);
        var oldest = await q.MinAsync(x => (DateTimeOffset?)x.CreatedAt, ct); return (count, oldest is null ? 0 : (now - oldest.Value).TotalSeconds);
    }
    public async Task<(long Pending, double OldestAgeSeconds)> GetDeliveryStatsAsync(DateTimeOffset now, CancellationToken ct)
    {
        var q = db.EmailNotifications.Where(x => x.Status == NotificationStatus.Queued || x.Status == NotificationStatus.RetryScheduled);
        var count = await q.LongCountAsync(ct); var oldest = await q.MinAsync(x => x.NextAttemptAt, ct);
        return (count, oldest is null ? 0 : Math.Max(0, (now - oldest.Value).TotalSeconds));
    }
}
