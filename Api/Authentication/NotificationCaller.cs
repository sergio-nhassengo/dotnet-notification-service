using Application.Notifications.Abstractions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace MPDCApiTemplate.Authentication;

public sealed class NotificationCaller(IHttpContextAccessor http) : INotificationCaller
{
    public string ApplicationId => http.HttpContext?.User.FindFirst("application_id")?.Value ?? "";
}
