using Domain.Common;
using Domain.Enums;

namespace Domain.Entities;

public sealed class EmailNotification : BaseEntity<Guid>
{
    private EmailNotification() { }

    public string ApplicationId { get; private set; } = "legacy";
    public string? RequestHash { get; private set; }
    public string? ContentJson { get; private set; }

    public void SetIntegrationContext(string applicationId, string? requestHash, string? contentJson) =>
        (ApplicationId, RequestHash, ContentJson) = (applicationId, requestHash, contentJson);

    public Guid MessageId { get; private set; }
    public string CorrelationId { get; private set; } = string.Empty;
    public string IdempotencyKey { get; private set; } = string.Empty;
    public string SenderEmail { get; private set; } = string.Empty;
    public string? SenderName { get; private set; }
    public string TemplateId { get; private set; } = string.Empty;
    public string TemplateVariables { get; private set; } = "{}";
    public string? Subject { get; private set; }
    public NotificationStatus Status { get; private set; }
    public DateTimeOffset? LastAttemptAt { get; private set; }
    public DateTimeOffset? SentAt { get; private set; }
    public DateTimeOffset? FailedAt { get; private set; }
    public int AttemptCount { get; private set; }
    public DateTimeOffset? NextAttemptAt { get; private set; }
    public string? LastErrorCode { get; private set; }
    public string? LastErrorMessage { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public static EmailNotification Create(Guid messageId, string correlationId, string idempotencyKey,
        string senderEmail, string? senderName,
        string templateId,  string templateVariables, string? subject,
        DateTimeOffset now)
    {
        return new EmailNotification
        {
            Id = Guid.NewGuid(),
            MessageId = messageId,
            CorrelationId = correlationId,
            IdempotencyKey = idempotencyKey,
            SenderEmail = senderEmail.Trim().ToLowerInvariant(),
            SenderName = senderName,
            TemplateId = templateId,
            TemplateVariables = templateVariables,
            Subject = subject,
            Status = NotificationStatus.Pending,
            NextAttemptAt = now,
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    public void MarkQueued(DateTimeOffset now) { Status = NotificationStatus.Queued; UpdatedAt = now; }
    public void RecordAttempt(DateTimeOffset? providerCalledAt, DateTimeOffset now)
    {
        AttemptCount++;
        if (providerCalledAt.HasValue) LastAttemptAt = providerCalledAt;
        UpdatedAt = now;
    }
    public void MarkSent(DateTimeOffset now)
    {
        Status = NotificationStatus.Sent; SentAt = now;
        NextAttemptAt = null; LastErrorCode = null; LastErrorMessage = null; UpdatedAt = now;
    }
    public void ScheduleRetry(string code, string message, DateTimeOffset nextAttemptAt, DateTimeOffset now)
    {
        Status = NotificationStatus.RetryScheduled; LastErrorCode = Truncate(code, 100); LastErrorMessage = Truncate(message, 500);
        NextAttemptAt = nextAttemptAt; UpdatedAt = now;
    }
    public void MarkPermanentlyFailed(string code, string message, DateTimeOffset now)
    {
        Status = NotificationStatus.PermanentlyFailed; FailedAt = now; LastErrorCode = Truncate(code, 100);
        LastErrorMessage = Truncate(message, 500); NextAttemptAt = null; UpdatedAt = now;
    }
    public void MarkDeadLettered(DateTimeOffset now) { Status = NotificationStatus.DeadLettered; UpdatedAt = now; }

    // Matches the persisted column lengths so a long error never makes the result save fail.
    private static string Truncate(string value, int maxLength) => value.Length > maxLength ? value[..maxLength] : value;
}
