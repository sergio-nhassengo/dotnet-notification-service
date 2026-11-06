using Application.Notifications.Abstractions;
using Application.Notifications.Models;
using Infrastructure.Notifications.Options;
using Microsoft.Extensions.Options;

namespace Infrastructure.Notifications.Integration;

public sealed class NotificationApplications(IOptions<NotificationIntegrationOptions> integration) : INotificationApplications
{
    public NotificationApplication? Find(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        if (!integration.Value.Applications.TryGetValue(id, out var app) || !app.Enabled) return null;
        return new(id, app.SenderEmail, app.SenderName, app.AllowedTemplateIds,
            app.AllowRawContent, app.AllowSubjectOverride, app.MaximumRecipients, app.MaximumAttachments, app.MaximumAttachmentBytes);
    }
}

