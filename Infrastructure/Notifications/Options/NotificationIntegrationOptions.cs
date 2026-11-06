using Microsoft.Extensions.Options;

namespace Infrastructure.Notifications.Options;

public sealed class NotificationIntegrationOptions
{
    public Dictionary<string, NotificationApplicationOptions> Applications { get; set; } = new();
}
