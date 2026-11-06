using Application.Notifications.Models;

namespace Application.Notifications.Abstractions;

public interface INotificationApplications
{
    NotificationApplication? Find(string applicationId);
}
