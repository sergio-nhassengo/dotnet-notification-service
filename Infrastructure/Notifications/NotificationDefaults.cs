using Application.Notifications.Abstractions;
using Infrastructure.Notifications.Options;
using Microsoft.Extensions.Options;

namespace Infrastructure.Notifications;

public sealed class NotificationDefaults(IOptions<KafkaOptions> kafka, IOptions<EmailDeliveryOptions> delivery) : INotificationDefaults
{
    private readonly KafkaOptions _kafka = kafka.Value;
    public string EmailRequestedTopic => _kafka.EmailRequestedTopic;
    public string DeadLetterTopic => _kafka.DeadLetterTopic;
    public int MaximumDeliveryAttempts => delivery.Value.MaximumAttempts;
}
