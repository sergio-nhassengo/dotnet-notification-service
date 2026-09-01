using System.Security.Cryptography;
using System.Text;
using Application.Common.Interfaces;
using Application.Notifications.Contracts;
using Application.Notifications.Interfaces;
using Application.Notifications.Security;
using Confluent.Kafka;
using Domain.Entities;
using Domain.Enums;
using Infrastructure.Notifications.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Infrastructure.Notifications.Workers;

public sealed class KafkaEmailConsumerWorker(IConsumer<string, string> consumer, IServiceScopeFactory scopes,
    IIntegrationEventSerializer serializer, INotificationDefaults defaults, IOptions<KafkaOptions> options,
    IDateTime clock, NotificationMetrics metrics, ILogger<KafkaEmailConsumerWorker> logger) : BackgroundService
{
    protected override Task ExecuteAsync(CancellationToken stoppingToken) => Task.Run(() => Consume(stoppingToken), stoppingToken);
    private async Task Consume(CancellationToken ct)
    {
        consumer.Subscribe(options.Value.EmailRequestedTopic);
        try
        {
            while (!ct.IsCancellationRequested)
            {
                ConsumeResult<string, string> item;
                try { item = consumer.Consume(ct); }
                catch (ConsumeException ex) { logger.LogError(ex, "Kafka consume failed for {KafkaTopic}", options.Value.EmailRequestedTopic); await Task.Delay(1000, ct); continue; }
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
                if (serializer.TryDeserializeEmailRequested(item.Message.Value ?? string.Empty, schema, out var contract, out var error) && contract is not null &&
                    Valid(contract, out error) && contract.MessageId == messageId && defaults.IsTemplateAllowed(contract.TemplateId) &&
                    (contract.Subject is null || defaults.AllowSubjectOverride))
                {
                    var priority = Enum.Parse<NotificationPriority>(contract.Priority, true);
                    notification = EmailNotification.Create(contract.MessageId, contract.CorrelationId, contract.IdempotencyKey,
                        contract.RecipientEmail, contract.RecipientName, defaults.SenderEmail, defaults.SenderName, defaults.ReplyTo,
                        contract.TemplateId, contract.TemplateVersion, serializer.Serialize(contract.Variables), contract.Subject,
                        priority, NotificationSource.Kafka, contract.RequestedAt, contract.ScheduledAt, clock.Now);
                    notification.SetKafkaOrigin(item.Topic, item.Partition.Value, item.Offset.Value);
                }
                else
                {
                    var safe = SafeError.Sanitize(error);
                    var dead = new EmailDeadLetteredV1(messageId, Header(item.Message.Headers, "correlation-id") ?? "unknown",
                        item.Topic, item.Partition.Value, item.Offset.Value, "InvalidContract", "Kafka.InvalidContract", safe, 0, clock.Now, Guid.Empty);
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
                await scope.ServiceProvider.GetRequiredService<INotificationStore>().AcceptKafkaAsync(notification, inbox, invalidDlq, ct);
                consumer.StoreOffset(item); consumer.Commit(item);
                try
                {
                    var watermark = consumer.QueryWatermarkOffsets(item.TopicPartition, TimeSpan.FromSeconds(1));
                    metrics.SetConsumerLag(watermark.High.Value - item.Offset.Value - 1);
                }
                catch (KafkaException) { }
                if (notification is not null) NotificationTelemetry.Requested.Add(1);
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
        finally { consumer.Close(); }
    }
    private static bool Valid(EmailRequestedV1 x, out string error)
    {
        if (x.MessageId == Guid.Empty || string.IsNullOrWhiteSpace(x.CorrelationId) || x.CorrelationId.Length > 200 ||
            string.IsNullOrWhiteSpace(x.IdempotencyKey) || x.IdempotencyKey.Length > 200 ||
            !System.Net.Mail.MailAddress.TryCreate(x.RecipientEmail, out _) || string.IsNullOrWhiteSpace(x.TemplateId) ||
            x.TemplateVersion < 1 || x.Variables.Count > 50 || !Enum.TryParse<NotificationPriority>(x.Priority, true, out _))
        { error = "Contract fields failed validation."; return false; }
        error = string.Empty; return true;
    }
    private static string? Header(Headers headers, string name) => headers.TryGetLastBytes(name, out var bytes) ? Encoding.UTF8.GetString(bytes) : null;
    private static Guid HashGuid(string? value) => new(SHA256.HashData(Encoding.UTF8.GetBytes(value ?? string.Empty)).AsSpan(0, 16));
}
