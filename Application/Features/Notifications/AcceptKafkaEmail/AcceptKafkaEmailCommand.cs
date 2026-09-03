using Application.Common.Interfaces;
using Application.Common.Persistence;
using Domain.Entities;
using Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Notifications.Commands.AcceptKafkaEmail;

public sealed record AcceptKafkaEmailCommand(EmailNotification? Notification, InboxMessage Inbox,
    OutboxMessage? InvalidDlq) : IRequest<bool>;

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
                x.MessageId == candidate.MessageId || x.IdempotencyKey == candidate.IdempotencyKey,
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
            db.OutboxMessages.Add(request.InvalidDlq);

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
