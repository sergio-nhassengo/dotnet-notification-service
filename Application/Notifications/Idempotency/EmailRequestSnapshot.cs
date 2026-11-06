using System.Text.Json;
using Application.Notifications.Models;
using Domain.Entities;

namespace Application.Notifications.Idempotency;

/// <summary>
/// The request content that defines an email's idempotency fingerprint. REST requests, Kafka events and
/// stored notifications are all converted to this shape so the hash never depends on the API request type.
/// </summary>
public sealed record EmailRequestSnapshot(
    string CorrelationId,
    string TemplateId,
    IReadOnlyDictionary<string, string> Variables,
    string? Subject,
    EmailContent Content)
{
    /// <summary>Rebuilds the snapshot from persisted columns; null for rows that predate stored content.</summary>
    public static EmailRequestSnapshot? FromStored(EmailNotification notification)
    {
        if (notification.ContentJson is null)
            return null;
        var content = JsonSerializer.Deserialize<EmailContent>(notification.ContentJson);
        if (content is null)
            return null;
        var variables = JsonSerializer.Deserialize<Dictionary<string, string>>(notification.TemplateVariables)
            ?? new Dictionary<string, string>();
        return new EmailRequestSnapshot(notification.CorrelationId, notification.TemplateId, variables,
            notification.Subject, content);
    }
}
