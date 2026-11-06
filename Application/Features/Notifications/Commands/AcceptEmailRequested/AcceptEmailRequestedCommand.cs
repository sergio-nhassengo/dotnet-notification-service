using System.Security.Cryptography;
using System.Text;
using Application.Common.Interfaces;
using Application.Notifications.Abstractions;
using Application.Notifications.Contracts;
using Application.Notifications.Idempotency;
using Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Application.Features.Notifications.Commands.AcceptEmailRequested;

/// <summary>
/// Accepts one consumed email-requested event. Only events matching a durable REST-created email are accepted;
/// anything else is dead-lettered. The caller may commit the source offset only after this completes.
/// </summary>
public sealed record AcceptEmailRequestedCommand(string Topic, int Partition, long Offset, string? MessageKey, string? Payload)
    : IRequest;

public sealed class AcceptEmailRequestedCommandHandler(IApplicationDbContext db, IntegrationEventJsonSerializer serializer,
    INotificationDefaults defaults, IDateTime clock, ILogger<AcceptEmailRequestedCommandHandler> logger)
    : IRequestHandler<AcceptEmailRequestedCommand>
{
    public async Task Handle(AcceptEmailRequestedCommand request, CancellationToken ct)
    {
        var payload = request.Payload ?? "";
        var keyValid = Guid.TryParse(request.MessageKey, out var messageId) && messageId != Guid.Empty;
        if (!keyValid)
            messageId = new Guid(SHA256.HashData(Encoding.UTF8.GetBytes(payload)).AsSpan(0, 16));
        var valid = serializer.TryDeserializeEmailRequested(payload, out var contract, out var error);
        valid = valid && keyValid && contract!.MessageId == messageId && contract.RequestedAt != default &&
            !string.IsNullOrWhiteSpace(contract.ApplicationId) && !string.IsNullOrWhiteSpace(contract.RequestHash) &&
            contract.Content is not null;
        var notification = valid
            ? await db.EmailNotifications.SingleOrDefaultAsync(x => x.MessageId == messageId, ct)
            : null;
        // Matching the stored hash proves the event carries exactly the content validated at REST submission.
        valid = valid && notification is not null && notification.ApplicationId == contract!.ApplicationId &&
            notification.IdempotencyKey == contract.IdempotencyKey && notification.RequestHash == contract.RequestHash &&
            EmailRequestFingerprint.Matches(notification, contract.IdempotencyKey, ToSnapshot(contract));
        if (valid && notification!.Status == NotificationStatus.Pending)
            notification.MarkQueued(clock.Now);
        else if (valid)
            valid = notification!.Status is NotificationStatus.Queued or NotificationStatus.RetryScheduled or
                NotificationStatus.Sent or NotificationStatus.PermanentlyFailed or NotificationStatus.DeadLettered;

        if (!valid)
        {
            if (string.IsNullOrWhiteSpace(error))
                error = "Event does not match an accepted REST notification.";
            var dead = new EmailDeadLetteredV1(messageId, contract?.CorrelationId ?? "unknown", request.Topic, request.Partition,
                request.Offset, "InvalidContract", "Kafka.InvalidContract", error, 0, clock.Now, Guid.Empty);
            db.OutboxMessages.Add(DeadLetterOutbox.Create(dead, serializer, defaults, clock.Now));
            logger.LogWarning("Invalid Kafka event: MessageId {MessageId}, Topic {Topic}, Partition {Partition}, Offset {Offset}, ErrorCode {ErrorCode}",
                messageId, request.Topic, request.Partition, request.Offset, "Kafka.InvalidContract");
        }
        // Duplicate events also save, so the consumer commits only after persistence succeeds.
        await db.SaveChangesAsync(ct);
    }

    private static EmailRequestSnapshot ToSnapshot(EmailRequestedV1 contract) => new(contract.CorrelationId,
        contract.TemplateId ?? "", contract.Variables ?? new Dictionary<string, string>(), contract.Subject, contract.Content!);
}
