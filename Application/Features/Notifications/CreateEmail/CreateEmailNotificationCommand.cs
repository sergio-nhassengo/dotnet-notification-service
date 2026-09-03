using System.Text.Json;
using Application.Common.Interfaces;
using Application.Common.Persistence;
using Application.Notifications.Contracts;
using Application.Notifications.Interfaces;
using Domain.Common;
using Domain.Entities;
using Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Notifications.Commands.CreateEmail;

public sealed record EmailRecipient(string Email, string? Name);
public sealed record CreateEmailNotificationCommand(string IdempotencyKey, string CorrelationId,
    EmailRecipient Recipient, string TemplateId, int TemplateVersion,
    IReadOnlyDictionary<string, string> Variables, string? Subject, string Priority,
    DateTimeOffset? ScheduledAt) : IRequest<Result<EmailAcceptedResponse>>;
public sealed record EmailAcceptedResponse(Guid NotificationId, Guid MessageId, string Status);

public sealed class CreateEmailNotificationCommandHandler(
    IApplicationDbContext db, IIntegrationEventSerializer serializer,
    INotificationDefaults defaults, IDateTime clock)
    : IRequestHandler<CreateEmailNotificationCommand, Result<EmailAcceptedResponse>>
{
    public async Task<Result<EmailAcceptedResponse>> Handle(CreateEmailNotificationCommand request, CancellationToken cancellationToken)
    {
        if (!defaults.IsTemplateAllowed(request.TemplateId))
            return Result.Failure<EmailAcceptedResponse>(Error.Unauthorized("Notification.TemplateNotAllowed", "The template is not allowed for this caller."));

        var existing = await db.EmailNotifications.AsNoTracking()
            .SingleOrDefaultAsync(x => x.IdempotencyKey == request.IdempotencyKey, cancellationToken);
        if (existing is not null)
            return new EmailAcceptedResponse(existing.Id, existing.MessageId, existing.Status.ToString());

        var now = clock.Now;
        var messageId = Guid.NewGuid();
        var priority = Enum.Parse<NotificationPriority>(request.Priority, true);
        var integrationEvent = new EmailRequestedV1(messageId, request.CorrelationId, request.IdempotencyKey,
            request.Recipient.Email, request.Recipient.Name, request.TemplateId, request.TemplateVersion,
            request.Variables, defaults.AllowSubjectOverride ? request.Subject : null, priority.ToString(), now, request.ScheduledAt);
        var payload = serializer.Serialize(integrationEvent);
        var notification = EmailNotification.Create(messageId, request.CorrelationId, request.IdempotencyKey,
            request.Recipient.Email, request.Recipient.Name, defaults.SenderEmail, defaults.SenderName,
            defaults.ReplyTo, request.TemplateId, request.TemplateVersion,
            JsonSerializer.Serialize(request.Variables), integrationEvent.Subject, priority,
            NotificationSource.RestApi, now, request.ScheduledAt, now);
        var outbox = new OutboxMessage
        {
            Id = Guid.NewGuid(),
            EventType = EmailRequestedV1.EventType,
            SchemaVersion = EmailRequestedV1.SchemaVersion,
            MessageKey = messageId,
            Topic = defaults.EmailRequestedTopic,
            Payload = payload,
            Headers = serializer.Serialize(new Dictionary<string, string>
            {
                ["correlation-id"] = request.CorrelationId,
                ["causation-id"] = messageId.ToString(),
                ["schema-version"] = EmailRequestedV1.SchemaVersion.ToString()
            }),
            OccurredAt = now,
            NextAttemptAt = now,
            CreatedAt = now
        };

        db.EmailNotifications.Add(notification);
        db.OutboxMessages.Add(outbox);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return new EmailAcceptedResponse(notification.Id, notification.MessageId, notification.Status.ToString());
        }
        catch (DbUpdateException exception) when (UniqueConstraintViolation.IsExpected(exception))
        {
            db.ClearTrackedChanges();
            existing = await db.EmailNotifications.AsNoTracking()
                .SingleAsync(x => x.IdempotencyKey == request.IdempotencyKey, cancellationToken);
            return new EmailAcceptedResponse(existing.Id, existing.MessageId, existing.Status.ToString());
        }
    }
}
