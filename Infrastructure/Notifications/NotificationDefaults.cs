using Application.Notifications.Interfaces;
using Infrastructure.Notifications.Options;
using Microsoft.Extensions.Options;

namespace Infrastructure.Notifications;

public sealed class NotificationDefaults(IOptions<KafkaOptions> kafka, IOptions<EmailProviderOptions> provider) : INotificationDefaults
{
    private readonly KafkaOptions _kafka = kafka.Value;
    private readonly EmailProviderOptions _provider = provider.Value;
    public string EmailRequestedTopic => _kafka.EmailRequestedTopic;
    public string DeadLetterTopic => _kafka.DeadLetterTopic;
    public string SenderEmail => _provider.DefaultSenderEmail;
    public string? SenderName => _provider.DefaultSenderName;
    public string? ReplyTo => _provider.ReplyTo;
    public bool AllowSubjectOverride => _provider.AllowSubjectOverride;
    public bool IsTemplateAllowed(string templateId) => _provider.AllowedTemplateIds.Contains(templateId, StringComparer.OrdinalIgnoreCase);
}
