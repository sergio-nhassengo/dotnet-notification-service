using Domain.Common;

namespace Application.Notifications.Models;

public sealed record NotificationApplication(string Id, string SenderEmail, string? SenderName,
    IReadOnlyList<string> TemplateIds, bool AllowRawContent, bool AllowSubjectOverride, int MaximumRecipients);
