using System.ComponentModel.DataAnnotations;

namespace Infrastructure.Notifications.Options;

public sealed class NotificationAttachmentOptions
{
    public const string SectionName = "NotificationAttachments";
    [Range(1, 3650)] public int RetentionDays { get; set; } = 7;
}
