using Application.Notifications.Contracts;
using Application.Notifications.Models;
using Domain.Entities;

namespace Application.Notifications.Interfaces;

public interface IEmailProvider
{
    string Name { get; }
    Task<EmailProviderResult> SendAsync(EmailMessage message, CancellationToken cancellationToken);
}

public interface IEmailTemplateRenderer
{
    Task<RenderedEmail> RenderAsync(string templateId, int version,
        IReadOnlyDictionary<string, string> variables, CancellationToken cancellationToken);
}

public interface IIntegrationEventSerializer
{
    string Serialize<T>(T value);
    bool TryDeserializeEmailRequested(string payload, int schemaVersion, out EmailRequestedV1? value, out string error);
}

public interface IKafkaPublisher
{
    Task PublishAsync(KafkaEnvelope message, CancellationToken cancellationToken);
}

public interface INotificationDefaults
{
    string EmailRequestedTopic { get; }
    string DeadLetterTopic { get; }
    string SenderEmail { get; }
    string? SenderName { get; }
    string? ReplyTo { get; }
    bool IsTemplateAllowed(string templateId);
    bool AllowSubjectOverride { get; }
}

public interface INotificationStore
{
    Task<EmailNotification> AcceptRestAsync(EmailNotification notification, OutboxMessage outbox, CancellationToken cancellationToken);
    Task<bool> AcceptKafkaAsync(EmailNotification? notification, InboxMessage inbox, OutboxMessage? invalidDlq, CancellationToken cancellationToken);
    Task<EmailNotification?> FindAsync(Guid id, CancellationToken cancellationToken);
    Task<IReadOnlyList<DeliveryAttempt>> GetAttemptsAsync(Guid notificationId, CancellationToken cancellationToken);
    Task<bool> ReplayAsync(Guid id, string actor, DateTimeOffset now, CancellationToken cancellationToken);
    Task<IReadOnlyList<OutboxMessage>> ClaimOutboxAsync(string owner, int batchSize, DateTimeOffset now, TimeSpan lease, CancellationToken cancellationToken);
    Task MarkOutboxProcessedAsync(Guid id, DateTimeOffset now, CancellationToken cancellationToken);
    Task MarkOutboxFailedAsync(Guid id, string safeError, DateTimeOffset nextAttemptAt, CancellationToken cancellationToken);
    Task<IReadOnlyList<EmailNotification>> ClaimDeliveriesAsync(string owner, int batchSize, DateTimeOffset now, TimeSpan lease, CancellationToken cancellationToken);
    Task RecordDeliveryResultAsync(Guid notificationId, string provider, DateTimeOffset startedAt,
        EmailProviderResult result, DateTimeOffset now, DateTimeOffset? nextAttemptAt,
        OutboxMessage? dlqOutbox, CancellationToken cancellationToken);
    Task<int> CleanupOutboxAsync(DateTimeOffset before, CancellationToken cancellationToken);
    Task<(long Pending, double OldestAgeSeconds)> GetOutboxStatsAsync(DateTimeOffset now, CancellationToken cancellationToken);
    Task<(long Pending, double OldestAgeSeconds)> GetDeliveryStatsAsync(DateTimeOffset now, CancellationToken cancellationToken);
}
