using Application.Common.Interfaces;
using Application.Common.Persistence;
using Domain.Entities;
using Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Notifications.Commands.AcceptKafkaEmail;

public sealed record AcceptKafkaEmailCommand(EmailNotification? Notification, InboxMessage Inbox,
    OutboxMessage? InvalidDlq, Guid? RejectedNotificationId = null) : IRequest<bool>;

public sealed class AcceptKafkaEmailCommandHandler(IApplicationDbContext db)
    : IRequestHandler<AcceptKafkaEmailCommand, bool>
{
    public async Task<bool> Handle(AcceptKafkaEmailCommand request, CancellationToken cancellationToken)
    {
        if (await db.InboxMessages.AnyAsync(x => x.ConsumerName == request.Inbox.ConsumerName &&
                x.MessageId == request.Inbox.MessageId, cancellationToken))
            return false;

        request.Inbox.ProcessedAt = DateTimeOffset.UtcNow;
        db.InboxMessages.Add(request.Inbox);
        if (request.Notification is not null)
        {
            var candidate = request.Notification;
            var existing = await db.EmailNotifications.FirstOrDefaultAsync(x =>
                x.MessageId == candidate.MessageId || (x.ApplicationId == candidate.ApplicationId && x.IdempotencyKey == candidate.IdempotencyKey),
                cancellationToken);
            if (existing is null)
            {
                candidate.MarkQueued(request.Inbox.ReceivedAt);
                db.EmailNotifications.Add(candidate);
            }
            else if (existing.Status == NotificationStatus.Pending)
            {
                existing.MarkQueued(request.Inbox.ReceivedAt);
            }
        }

        if (request.InvalidDlq is not null)
        {
            db.OutboxMessages.Add(request.InvalidDlq);
            if (request.RejectedNotificationId is not null)
            {
                var rejected = await db.EmailNotifications.SingleOrDefaultAsync(x => x.Id == request.RejectedNotificationId && x.Status == NotificationStatus.Pending, cancellationToken);
                if (rejected is not null)
                {
                    rejected.MarkPermanentlyFailed("Kafka.ValidationFailed", "The queued request no longer passes validation.", request.Inbox.ReceivedAt);
                    rejected.MarkDeadLettered(request.Inbox.ReceivedAt);
                }
            }
        }

        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException exception) when (UniqueConstraintViolation.IsExpected(exception))
        {
            db.ClearTrackedChanges();
            return false;
        }
    }
}
