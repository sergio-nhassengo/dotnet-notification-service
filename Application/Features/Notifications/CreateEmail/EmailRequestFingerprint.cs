using System.Text.Json;
using Application.Notifications.Integration;
using Domain.Entities;
using System.Security.Cryptography;
using System.Text;
using Application.Common.Messaging;

namespace Application.Features.Notifications.Commands.CreateEmail;

public static class EmailRequestFingerprint
{
    public static string Compute(CreateEmailNotificationCommand request) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(new IntegrationEventJsonSerializer().Serialize(new
        {
            request.BusinessReference, request.CorrelationId, TemplateId = request.TemplateId ?? "",
            Variables = (request.Variables ?? new Dictionary<string, string>())
                .OrderBy(x => x.Key, StringComparer.Ordinal).ToArray(),
            request.Subject, ScheduledAt = request.ScheduledAt?.ToUniversalTime(), Content = request.Content()
        }))));

    public static bool Matches(EmailNotification existing, CreateEmailNotificationCommand request)
    {
        if (existing.IdempotencyKey != request.IdempotencyKey) 
            return false;
        if (existing.RequestHash is null) 
            return true; // Preserve historical rows that predate request fingerprints.
        var hash = Compute(request);
        if (existing.RequestHash == hash) 
            return true;
        if (existing.ContentJson is null) 
            return false;
        // Older fingerprints included template version. Compare durable request content rather than invalidating retries.
        var content = JsonSerializer.Deserialize<EmailContent>(existing.ContentJson);
        if (content is null) 
            return false;
        var stored = new CreateEmailNotificationCommand(existing.IdempotencyKey, existing.CorrelationId, null,
            existing.TemplateId, JsonSerializer.Deserialize<Dictionary<string, string>>(existing.TemplateVariables),
            existing.Subject, existing.ScheduledAt)
        {
            BusinessReference = existing.BusinessReference, To = content.To, Cc = content.Cc,
            Attachments = content.Attachments, HtmlBody = content.HtmlBody, TextBody = content.TextBody
        };
        return Compute(stored) == hash;
    }
}
