using System.Security.Cryptography;
using System.Text;
using Application.Notifications.Contracts;
using Domain.Entities;

namespace Application.Notifications.Idempotency;

public static class EmailRequestFingerprint
{
    // The serialized shape below is persisted as RequestHash. Changing property names, order or
    // serializer options invalidates every stored fingerprint (see EmailRequestFingerprintTests).
    public static string Compute(EmailRequestSnapshot request) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(new IntegrationEventJsonSerializer().Serialize(new
        {
            request.CorrelationId,
            request.TemplateId,
            Variables = request.Variables.OrderBy(x => x.Key, StringComparer.Ordinal).ToArray(),
            request.Subject,
            // Attachments were removed, but stored hashes include an empty "attachments" array at this
            // position. Keep emitting it so existing fingerprints still match.
            Content = new
            {
                request.Content.To,
                request.Content.Cc,
                Attachments = Array.Empty<object>(),
                request.Content.HtmlBody,
                request.Content.TextBody
            }
        }))));

    public static bool Matches(EmailNotification existing, string idempotencyKey, EmailRequestSnapshot request)
    {
        if (existing.IdempotencyKey != idempotencyKey)
            return false;
        if (existing.RequestHash is null)
            return true; // Preserve historical rows that predate request fingerprints.
        var hash = Compute(request);
        if (existing.RequestHash == hash)
            return true;
        // Older fingerprints included template version or business reference. Compare durable request content rather than invalidating retries.
        var stored = EmailRequestSnapshot.FromStored(existing);
        return stored is not null && Compute(stored) == hash;
    }
}
