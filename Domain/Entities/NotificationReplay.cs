using Domain.Common;

namespace Domain.Entities;

public sealed class NotificationReplay : BaseEntity<Guid>
{
    public Guid NotificationId { get; set; }
    public EmailNotification Notification { get; set; } = null!;
    public string RequestedBy { get; set; } = string.Empty;
    public DateTimeOffset RequestedAt { get; set; }
    public int PreviousAttemptCount { get; set; }
}
