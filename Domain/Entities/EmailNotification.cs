using Domain.Common;
using Domain.Enums;

namespace Domain.Entities;

public sealed class EmailNotification : BaseEntity<Guid>
{
    private EmailNotification() { }

    public Guid MessageId { get; private set; }
    public string CorrelationId { get; private set; } = string.Empty;
    public string IdempotencyKey { get; private set; } = string.Empty;
    public string RecipientEmail { get; private set; } = string.Empty;
    public string? RecipientName { get; private set; }
    public string SenderEmail { get; private set; } = string.Empty;
    public string? SenderName { get; private set; }
    public string? ReplyTo { get; private set; }
    public string TemplateId { get; private set; } = string.Empty;
    public int TemplateVersion { get; private set; }
    public string TemplateVariables { get; private set; } = "{}";
    public string? Subject { get; private set; }
    public NotificationPriority Priority { get; private set; }
    public NotificationStatus Status { get; private set; }
    public NotificationSource Source { get; private set; }
    public DateTimeOffset RequestedAt { get; private set; }
    public DateTimeOffset? ScheduledAt { get; private set; }
    public DateTimeOffset? SentAt { get; private set; }
    public DateTimeOffset? FailedAt { get; private set; }
    public string? ProviderMessageId { get; private set; }
    public int AttemptCount { get; private set; }
    public DateTimeOffset? NextAttemptAt { get; private set; }
    public string? LastErrorCode { get; private set; }
    public string? LastErrorMessage { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public string? OriginalTopic { get; private set; }
    public int? OriginalPartition { get; private set; }
    public long? OriginalOffset { get; private set; }
    public string? LeaseOwner { get; private set; }
    public DateTimeOffset? LeaseExpiresAt { get; private set; }
    public byte[] RowVersion { get; private set; } = [];
    public ICollection<DeliveryAttempt> DeliveryAttempts { get; private set; } = [];

    public static EmailNotification Create(Guid messageId, string correlationId, string idempotencyKey,
        string recipientEmail, string? recipientName, string senderEmail, string? senderName, string? replyTo,
        string templateId, int templateVersion, string templateVariables, string? subject,
        NotificationPriority priority, NotificationSource source, DateTimeOffset requestedAt,
        DateTimeOffset? scheduledAt, DateTimeOffset now)
    {
        return new EmailNotification
        {
            Id = Guid.NewGuid(),
            MessageId = messageId,
            CorrelationId = correlationId,
            IdempotencyKey = idempotencyKey,
            RecipientEmail = recipientEmail.Trim().ToLowerInvariant(),
            RecipientName = recipientName,
            SenderEmail = senderEmail.Trim().ToLowerInvariant(),
            SenderName = senderName,
            ReplyTo = replyTo,
            TemplateId = templateId,
            TemplateVersion = templateVersion,
            TemplateVariables = templateVariables,
            Subject = subject,
            Priority = priority,
            Source = source,
            RequestedAt = requestedAt,
            ScheduledAt = scheduledAt,
            Status = NotificationStatus.Pending,
            NextAttemptAt = scheduledAt ?? now,
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    public void SetKafkaOrigin(string topic, int partition, long offset) =>
        (OriginalTopic, OriginalPartition, OriginalOffset) = (topic, partition, offset);

    public void MarkQueued(DateTimeOffset now) { Status = NotificationStatus.Queued; UpdatedAt = now; }
    public void StartAttempt(DateTimeOffset now) { Status = NotificationStatus.Processing; AttemptCount++; UpdatedAt = now; }
    public void MarkSent(string? providerMessageId, DateTimeOffset now)
    {
        Status = NotificationStatus.Sent; SentAt = now; ProviderMessageId = providerMessageId;
        NextAttemptAt = null; LastErrorCode = null; LastErrorMessage = null; ClearLease(); UpdatedAt = now;
    }
    public void ScheduleRetry(string code, string message, DateTimeOffset nextAttemptAt, DateTimeOffset now)
    {
        Status = NotificationStatus.RetryScheduled; LastErrorCode = code; LastErrorMessage = message;
        NextAttemptAt = nextAttemptAt; ClearLease(); UpdatedAt = now;
    }
    public void MarkPermanentlyFailed(string code, string message, DateTimeOffset now)
    {
        Status = NotificationStatus.PermanentlyFailed; FailedAt = now; LastErrorCode = code;
        LastErrorMessage = message; NextAttemptAt = null; ClearLease(); UpdatedAt = now;
    }
    public void MarkDeadLettered(DateTimeOffset now) { Status = NotificationStatus.DeadLettered; ClearLease(); UpdatedAt = now; }
    public void Replay(DateTimeOffset now)
    {
        if (Status is not (NotificationStatus.DeadLettered or NotificationStatus.PermanentlyFailed))
            throw new InvalidOperationException("Only failed notifications can be replayed.");
        Status = NotificationStatus.RetryScheduled; NextAttemptAt = now; FailedAt = null;
        LastErrorCode = null; LastErrorMessage = null; ClearLease(); UpdatedAt = now;
    }
    public void Claim(string owner, DateTimeOffset until) { LeaseOwner = owner; LeaseExpiresAt = until; }
    public void ClearLease() { LeaseOwner = null; LeaseExpiresAt = null; }
}
