namespace Application.Notifications.Abstractions;

public interface INotificationDefaults
{
    string EmailRequestedTopic { get; }
    string DeadLetterTopic { get; }
    int MaximumDeliveryAttempts { get; }

}
