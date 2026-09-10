using System.Text.Json;
using Application.Features.Notifications.Commands.CreateEmail;
using Application.Notifications.Integration;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;
using Application.Common.Interfaces;
using Application.Common.Messaging;
using Application.Common.Notifications;
using Application.Features.Notifications.Commands.AcceptKafkaEmail;
using Application.Notifications.Contracts;
using Confluent.Kafka;
using Domain.Entities;
using Domain.Enums;
using Infrastructure.Notifications.Options;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Infrastructure.Notifications.Workers;

public sealed class KafkaEmailMessageProcessor(IServiceScopeFactory scopes,
    IntegrationEventJsonSerializer serializer, INotificationDefaults defaults, IOptions<KafkaOptions> options,
    IDateTime clock, ILogger<KafkaEmailMessageProcessor> logger)
{
    public async Task ProcessAsync(ConsumeResult<string, string> item, CancellationToken ct)
    {
        var schemaText = Header(item.Message.Headers, "schema-version");
        var schema = int.TryParse(schemaText, out var parsed) ? parsed : 0;
        var messageId = Guid.TryParse(item.Message.Key, out var key) ? key : HashGuid(item.Message.Value);
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(item.Message.Value ?? string.Empty)));
        var inbox = new InboxMessage
        {
            Id = Guid.NewGuid(),
            MessageId = messageId,
            ConsumerName = options.Value.ConsumerGroup,
            Topic = item.Topic,
            Partition = item.Partition.Value,
            Offset = item.Offset.Value,
            ReceivedAt = clock.Now,
            PayloadHash = hash
        };
        EmailNotification? notification = null; OutboxMessage? invalidDlq = null;
        var error = "Contract fields failed validation.";
        Guid? rejectedNotificationId = null;
        using var validationScope = scopes.CreateScope();
        var db = validationScope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        var apps = validationScope.ServiceProvider.GetRequiredService<INotificationApplications>();
        serializer.TryDeserializeEmailRequested(item.Message.Value ?? "", schema, out var contract, out error);
        var appId = contract?.ApplicationId ?? "legacy";
        var app = apps.Find(appId);
        var content = contract?.Content ?? new EmailContent
        {
            To = contract is null ? [] : [new(contract.RecipientEmail, contract.RecipientName)]
        };
        var request = contract is null ? null : new CreateEmailNotificationCommand(contract.IdempotencyKey,
            contract.CorrelationId, null, contract.TemplateId, contract.Variables,
            contract.Subject, contract.ScheduledAt)
        {
            To = content.To, Cc = content.Cc, Attachments = content.Attachments,
            HtmlBody = content.HtmlBody, TextBody = content.TextBody, BusinessReference = contract?.BusinessReference
        };
        // Application identity cannot be asserted by a publisher on the shared internal topic.
        // Application-scoped content is accepted only when it matches a durable REST submission, even after outbox cleanup.
        var scoped = contract?.ApplicationId is not null || contract?.Content is not null || contract?.RequestHash is not null || contract?.BusinessReference is not null;
        var trustedSubmission = !scoped;
        if (scoped && contract is not null && request is not null &&
            new CreateEmailNotificationCommandValidator().Validate(request).IsValid)
        {
            var submitted = await db.EmailNotifications.AsNoTracking().SingleOrDefaultAsync(x =>
                x.MessageId == messageId && x.ApplicationId == appId && x.Source == NotificationSource.RestApi &&
                x.RequestHash == contract.RequestHash, ct);
            trustedSubmission = submitted is not null && EmailRequestFingerprint.Matches(submitted, request);
        }
        var valid = app is not null && contract is not null && contract.ContractVersion == 1 &&
            contract.MessageId != Guid.Empty && contract.RequestedAt != default && contract.MessageId == messageId && request is not null &&
            new CreateEmailNotificationCommandValidator().Validate(request).IsValid && trustedSubmission;
        if (valid)
        {
            var preflight = validationScope.ServiceProvider.GetRequiredService<IEmailPreflight>();
            var failure = await preflight.ValidateAsync(app!, contract!.TemplateId, contract.Variables ?? new Dictionary<string, string>(), contract.Subject, content, ct);
            valid = failure is null;
            if (failure is not null)
            {
                error = failure.Message;
                if (scoped && trustedSubmission)
                    rejectedNotificationId = await db.EmailNotifications.Where(x => x.MessageId == messageId && x.Status == NotificationStatus.Pending)
                        .Select(x => (Guid?)x.Id).SingleOrDefaultAsync(ct);
            }
        }
        var requestHash = valid ? EmailRequestFingerprint.Compute(request!) : null;
        var existing = valid ? await db.EmailNotifications.AsNoTracking().FirstOrDefaultAsync(x =>
            x.MessageId == messageId || (x.ApplicationId == appId && x.IdempotencyKey == contract!.IdempotencyKey), ct) : null;
        if (existing is not null && (existing.ApplicationId != appId ||
            !EmailRequestFingerprint.Matches(existing, request!)))
        {
            valid = false;
            error = "Message ID or idempotency key conflicts with an existing notification.";
        }
        if (valid)
        {
            notification = EmailNotification.Create(contract!.MessageId, contract.CorrelationId, contract.IdempotencyKey,
                content.To[0].Email, content.To[0].Name, app!.SenderEmail, app.SenderName, app.ReplyTo,
                contract.TemplateId, JsonSerializer.Serialize(contract.Variables), contract.Subject,
                NotificationSource.Kafka, contract.RequestedAt, contract.ScheduledAt, clock.Now);
            notification.SetIntegrationContext(appId, contract?.BusinessReference, requestHash, JsonSerializer.Serialize(content));
            notification.SetKafkaOrigin(item.Topic, item.Partition.Value, item.Offset.Value);
        }
        else
        {
            if (string.IsNullOrWhiteSpace(error)) error = "Contract fields failed validation.";
            var dead = new EmailDeadLetteredV1(messageId, Header(item.Message.Headers, "correlation-id") ?? "unknown",
                item.Topic, item.Partition.Value, item.Offset.Value, "InvalidContract", "Kafka.InvalidContract", error, 0, clock.Now, rejectedNotificationId ?? Guid.Empty);
            invalidDlq = new OutboxMessage
            {
                Id = Guid.NewGuid(),
                EventType = EmailDeadLetteredV1.EventType,
                SchemaVersion = EmailDeadLetteredV1.SchemaVersion,
                MessageKey = messageId,
                Topic = defaults.DeadLetterTopic,
                Payload = serializer.Serialize(dead),
                Headers = serializer.Serialize(new Dictionary<string, string> { { "correlation-id", dead.CorrelationId }, { "causation-id", messageId.ToString() }, { "schema-version", "1" } }),
                OccurredAt = clock.Now,
                NextAttemptAt = clock.Now,
                CreatedAt = clock.Now
            };
            logger.LogWarning("Invalid Kafka notification dead-lettered: MessageId {MessageId}, KafkaTopic {KafkaTopic}, KafkaPartition {KafkaPartition}, KafkaOffset {KafkaOffset}, ErrorCode {ErrorCode}",
                messageId, item.Topic, item.Partition.Value, item.Offset.Value, "Kafka.InvalidContract");
        }
        using var scope = scopes.CreateScope();
        await scope.ServiceProvider.GetRequiredService<ISender>()
            .Send(new AcceptKafkaEmailCommand(notification, inbox, invalidDlq, rejectedNotificationId), ct);
    }
    private static string? Header(Headers? headers, string name) => headers is not null && headers.TryGetLastBytes(name, out var bytes) ? Encoding.UTF8.GetString(bytes) : null;
    private static Guid HashGuid(string? value) => new(SHA256.HashData(Encoding.UTF8.GetBytes(value ?? string.Empty)).AsSpan(0, 16));
}
