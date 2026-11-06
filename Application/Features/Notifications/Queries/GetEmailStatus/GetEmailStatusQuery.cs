using Application.Common.Interfaces;
using Application.Notifications.Abstractions;
using Domain.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Notifications.Queries.GetEmailStatus;

public sealed record GetEmailStatusQuery(Guid NotificationId) : IRequest<Result<EmailNotificationStatusDto>>;

public sealed class GetEmailStatusQueryHandler(IApplicationDbContext db, INotificationCaller caller, INotificationApplications applications)
    : IRequestHandler<GetEmailStatusQuery, Result<EmailNotificationStatusDto>>
{
    public async Task<Result<EmailNotificationStatusDto>> Handle(GetEmailStatusQuery request, CancellationToken cancellationToken)
    {
        var notification = await db.EmailNotifications.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == request.NotificationId && x.ApplicationId == caller.ApplicationId, cancellationToken);
        if (notification is null || applications.Find(caller.ApplicationId) is null)
            return Result.Failure<EmailNotificationStatusDto>(Error.EntityNotFound("EmailNotification", request.NotificationId));
        return new EmailNotificationStatusDto(notification.Id, notification.MessageId, notification.CorrelationId,
            notification.Status.ToString(), notification.AttemptCount, notification.CreatedAt,
            notification.LastAttemptAt, notification.SentAt, notification.FailedAt,
            notification.LastErrorCode, notification.LastErrorMessage,
            notification.NextAttemptAt) { ApplicationId = notification.ApplicationId };
    }
}
