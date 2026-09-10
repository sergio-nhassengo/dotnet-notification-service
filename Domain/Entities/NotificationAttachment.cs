using Domain.Common;

namespace Domain.Entities;

public sealed class NotificationAttachment : BaseEntity<Guid>
{
    public string ApplicationId { get; set; } = "legacy";
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public byte[] Content { get; set; } = [];
    public DateTimeOffset CreatedAt { get; set; }
}
