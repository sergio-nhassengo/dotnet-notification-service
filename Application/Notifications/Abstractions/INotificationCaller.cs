using Domain.Common;

namespace Application.Notifications.Abstractions;

public interface INotificationCaller
{
    string ApplicationId { get; }
}
