using System.Data;
using Application.Common.Interfaces;
using Application.Notifications.Models;
using Domain.Entities;
using Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Notifications.Commands.Delivery;

public sealed record ClaimEmailDeliveryBatchCommand(string Owner, int BatchSize, DateTimeOffset Now, TimeSpan Lease)
    : IRequest<IReadOnlyList<EmailNotification>>;

public sealed class ClaimEmailDeliveryBatchCommandHandler(IApplicationDbContext db, IApplicationTransaction transaction)
    : IRequestHandler<ClaimEmailDeliveryBatchCommand, IReadOnlyList<EmailNotification>>
{
    public Task<IReadOnlyList<EmailNotification>> Handle(ClaimEmailDeliveryBatchCommand request, CancellationToken cancellationToken) =>
        transaction.ExecuteAsync<IReadOnlyList<EmailNotification>>(IsolationLevel.Serializable, async ct =>
        {
            var states = new[] { NotificationStatus.Queued, NotificationStatus.RetryScheduled };
            var rows = await db.EmailNotifications.Where(x => states.Contains(x.Status) &&
                    x.NextAttemptAt.HasValue && x.NextAttemptAt.Value <= request.Now &&
                    (!x.LeaseExpiresAt.HasValue || x.LeaseExpiresAt.Value < request.Now))
                .OrderByDescending(x => x.Priority).ThenBy(x => x.NextAttemptAt)
                .Take(request.BatchSize).ToListAsync(ct);
            foreach (var row in rows)
                row.Claim(request.Owner, request.Now.Add(request.Lease));
            await db.SaveChangesAsync(ct);
            return rows;
        }, cancellationToken);
}

public sealed record RecordEmailDeliveryResultCommand(Guid NotificationId, string Provider,
    DateTimeOffset StartedAt, EmailProviderResult Result, DateTimeOffset Now,
    DateTimeOffset? NextAttemptAt, OutboxMessage? DlqOutbox) : IRequest;

public sealed class RecordEmailDeliveryResultCommandHandler(IApplicationDbContext db)
    : IRequestHandler<RecordEmailDeliveryResultCommand>
{
    public async Task Handle(RecordEmailDeliveryResultCommand request, CancellationToken cancellationToken)
    {
        var entity = await db.EmailNotifications.SingleAsync(x => x.Id == request.NotificationId, cancellationToken);
        entity.StartAttempt(request.StartedAt);
        var outcome = request.Result.IsSuccess ? DeliveryOutcome.Succeeded : request.Result.FailureCategory switch
        {
            EmailFailureCategory.Transient => DeliveryOutcome.TransientFailure,
            EmailFailureCategory.RateLimited => DeliveryOutcome.RateLimited,
            EmailFailureCategory.Configuration => DeliveryOutcome.ConfigurationFailure,
            _ => DeliveryOutcome.PermanentFailure
        };
        db.DeliveryAttempts.Add(new DeliveryAttempt
        {
            Id = Guid.NewGuid(),
            NotificationId = entity.Id,
            AttemptNumber = entity.AttemptCount,
            Provider = request.Provider,
            StartedAt = request.StartedAt,
            CompletedAt = request.Now,
            Outcome = outcome,
            ProviderStatusCode = request.Result.StatusCode,
            ProviderMessageId = request.Result.ProviderMessageId,
            ErrorCategory = request.Result.FailureCategory,
            ErrorCode = request.Result.ErrorCode,
            SafeErrorMessage = request.Result.SafeErrorMessage,
            NextAttemptAt = request.NextAttemptAt
        });
        if (request.Result.IsSuccess)
            entity.MarkSent(request.Result.ProviderMessageId, request.Now);
        else if (request.NextAttemptAt is not null)
            entity.ScheduleRetry(request.Result.ErrorCode ?? "TransientFailure",
                request.Result.SafeErrorMessage ?? "Delivery failed.", request.NextAttemptAt.Value, request.Now);
        else
        {
            entity.MarkPermanentlyFailed(request.Result.ErrorCode ?? "PermanentFailure",
                request.Result.SafeErrorMessage ?? "Delivery failed.", request.Now);
            if (request.DlqOutbox is not null)
            {
                db.OutboxMessages.Add(request.DlqOutbox);
                entity.MarkDeadLettered(request.Now);
            }
        }
        await db.SaveChangesAsync(cancellationToken);
    }
}
