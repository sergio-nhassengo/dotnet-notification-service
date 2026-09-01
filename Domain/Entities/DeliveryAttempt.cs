using Domain.Common;
using Domain.Enums;

namespace Domain.Entities;

public sealed class DeliveryAttempt : BaseEntity<Guid>
{
    public Guid NotificationId { get; set; }
    public EmailNotification Notification { get; set; } = null!;
    public int AttemptNumber { get; set; }
    public string Provider { get; set; } = string.Empty;
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public DeliveryOutcome Outcome { get; set; }
    public int? ProviderStatusCode { get; set; }
    public string? ProviderMessageId { get; set; }
    public EmailFailureCategory ErrorCategory { get; set; }
    public string? ErrorCode { get; set; }
    public string? SafeErrorMessage { get; set; }
    public DateTimeOffset? NextAttemptAt { get; set; }
}
