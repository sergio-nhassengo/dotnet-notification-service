using Application.Notifications.Models;
using Domain.Common;

namespace Application.Notifications.Abstractions;

public interface IEmailPreflight
{
    Task<Error?> ValidateAsync(NotificationApplication application, string? templateId,
        IReadOnlyDictionary<string, string> variables, string? subject, EmailContent content, CancellationToken ct);
}
