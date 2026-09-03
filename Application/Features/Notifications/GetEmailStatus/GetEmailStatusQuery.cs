using Application.Common.Interfaces;
using Domain.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Notifications.Queries.GetEmailStatus;

public sealed record DeliveryAttemptDto(int AttemptNumber, string Provider, DateTimeOffset StartedAt,
    DateTimeOffset? CompletedAt, string Outcome, int? ProviderStatusCode, string? ProviderMessageId,
    string? ErrorCode, string? SafeErrorMessage, DateTimeOffset? NextAttemptAt);
public sealed record EmailNotificationStatusDto(Guid NotificationId, Guid MessageId, string CorrelationId,
    string Status, int AttemptCount, DateTimeOffset RequestedAt, DateTimeOffset? ScheduledAt,
    DateTimeOffset? SentAt, DateTimeOffset? FailedAt, string? ProviderMessageId,
    string? LastErrorCode, string? LastErrorMessage, IReadOnlyList<DeliveryAttemptDto> Attempts);
public sealed record GetEmailStatusQuery(Guid NotificationId) : IRequest<Result<EmailNotificationStatusDto>>;

public sealed class GetEmailStatusQueryHandler(IApplicationDbContext db)
    : IRequestHandler<GetEmailStatusQuery, Result<EmailNotificationStatusDto>>
{
    public async Task<Result<EmailNotificationStatusDto>> Handle(GetEmailStatusQuery request, CancellationToken cancellationToken)
    {
        var notification = await db.EmailNotifications.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == request.NotificationId, cancellationToken);
        if (notification is null)
            return Result.Failure<EmailNotificationStatusDto>(Error.EntityNotFound("EmailNotification", request.NotificationId));
        var attempts = await db.DeliveryAttempts.AsNoTracking()
            .Where(x => x.NotificationId == request.NotificationId)
            .OrderBy(x => x.AttemptNumber).ToListAsync(cancellationToken);
        return new EmailNotificationStatusDto(notification.Id, notification.MessageId, notification.CorrelationId,
            notification.Status.ToString(), notification.AttemptCount, notification.RequestedAt,
            notification.ScheduledAt, notification.SentAt, notification.FailedAt, notification.ProviderMessageId,
            notification.LastErrorCode, notification.LastErrorMessage,
            attempts.Select(x => new DeliveryAttemptDto(x.AttemptNumber, x.Provider, x.StartedAt, x.CompletedAt,
                x.Outcome.ToString(), x.ProviderStatusCode, x.ProviderMessageId, x.ErrorCode,
                x.SafeErrorMessage, x.NextAttemptAt)).ToArray());
    }
}
