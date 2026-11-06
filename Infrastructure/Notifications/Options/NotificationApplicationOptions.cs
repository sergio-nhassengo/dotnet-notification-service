using Microsoft.Extensions.Options;

namespace Infrastructure.Notifications.Options;

public sealed class NotificationApplicationOptions
{
    public bool Enabled { get; set; } = true;
    public string ApiKeySha256 { get; set; } = "";
    public string SenderEmail { get; set; } = "";
    public string? SenderName { get; set; }
    public string[] AllowedTemplateIds { get; set; } = [];
    public bool AllowRawContent { get; set; }
    public bool AllowSubjectOverride { get; set; }
    public int MaximumRecipients { get; set; } = 50;
    public int MaximumAttachments { get; set; } = 5;
    public long MaximumAttachmentBytes { get; set; } = 5 * 1024 * 1024;
}
