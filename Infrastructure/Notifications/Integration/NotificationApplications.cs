using Application.Notifications.Integration;
using Infrastructure.Notifications.Options;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace Infrastructure.Notifications.Integration;

public sealed class NotificationIntegrationOptions
{
    public bool AllowLegacyJwtClients { get; set; } = true;
    public Dictionary<string, NotificationApplicationOptions> Applications { get; set; } = new();
}
public sealed class NotificationApplicationOptions
{
    public bool Enabled { get; set; } = true;
    public string ApiKeySha256 { get; set; } = "";
    public string SenderEmail { get; set; } = "";
    public string? SenderName { get; set; }
    public string? ReplyTo { get; set; }
    public string[] AllowedTemplateIds { get; set; } = [];
    public bool AllowRawContent { get; set; }
    public bool AllowSubjectOverride { get; set; }
    public int MaximumRecipients { get; set; } = 50;
    public int MaximumAttachments { get; set; } = 5;
    public long MaximumAttachmentBytes { get; set; } = 5 * 1024 * 1024;
}
public sealed class NotificationApplications(IOptions<NotificationIntegrationOptions> integration,
    IOptions<EmailProviderOptions> provider, IOptions<KafkaOptions> kafka) : INotificationApplications
{
    public bool UseKafka => kafka.Value.Enabled;
    public NotificationApplication? Find(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        if (id == "legacy" && integration.Value.AllowLegacyJwtClients)
        {
            var p = provider.Value;
            return new(id, p.DefaultSenderEmail, p.DefaultSenderName, p.ReplyTo,
                p.AllowedTemplateIds, false, p.AllowSubjectOverride, 1, 0, 0);
        }
        if (!integration.Value.Applications.TryGetValue(id, out var app) || !app.Enabled) return null;
        return new(id, app.SenderEmail, app.SenderName, app.ReplyTo, app.AllowedTemplateIds,
            app.AllowRawContent, app.AllowSubjectOverride, app.MaximumRecipients, app.MaximumAttachments, app.MaximumAttachmentBytes);
    }
}
public sealed class NotificationCaller(IHttpContextAccessor http) : INotificationCaller
{
    public string ApplicationId => http.HttpContext?.User.FindFirst("application_id")?.Value ?? "legacy";
    public bool IsAdmin => http.HttpContext?.User.IsInRole("Admin") == true;
}
