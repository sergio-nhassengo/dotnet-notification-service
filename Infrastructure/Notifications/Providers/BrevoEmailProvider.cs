using Application.Notifications.Integration;
using System.Net.Http.Json;
using System.Net;
using System.Text.Json;
using Application.Common.Email;
using Application.Notifications.Models;
using Infrastructure.Notifications.Options;
using Microsoft.Extensions.Options;

namespace Infrastructure.Notifications.Providers;

public sealed class BrevoEmailProvider(HttpClient client, IOptions<EmailProviderOptions> options) : IEmailProvider
{
    public string Name => "Brevo";
    public async Task<EmailProviderResult> SendAsync(EmailMessage message, CancellationToken ct)
    {
        if (message.Attachments.Any(x => x.ContentId is not null))
            return EmailProviderResult.Failure(Domain.Enums.EmailFailureCategory.Permanent, "Brevo.InlineUnsupported", "Inline attachments require the SMTP provider.");
        var recipients = message.To.Count > 0 ? message.To : new[] { new EmailAddress(message.RecipientEmail, message.RecipientName) };
        var body = new
        {
            sender = new { email = message.SenderEmail, name = message.SenderName },
            to = recipients.Select(x => new { email = x.Email, name = x.Name }).ToArray(),
            cc = message.Cc.Count == 0 ? null : message.Cc.Select(x => new { email = x.Email, name = x.Name }).ToArray(),
            attachment = message.Attachments.Count == 0 ? null : message.Attachments.Select(x => new { name = x.FileName, content = Convert.ToBase64String(x.Content) }).ToArray(),
            replyTo = message.ReplyTo is null ? null : new { email = message.ReplyTo },
            subject = message.Subject,
            htmlContent = string.IsNullOrWhiteSpace(message.HtmlBody) ? "<pre>" + WebUtility.HtmlEncode(message.TextBody) + "</pre>" : message.HtmlBody,
            textContent = message.TextBody,
            headers = new Dictionary<string, string>
            {
                ["X-Mailin-custom"] = $"message-id:{message.MessageId}",
                ["Idempotency-Key"] = message.MessageId.ToString()
            }
        };
        using var request = new HttpRequestMessage(HttpMethod.Post, "smtp/email") { Content = JsonContent.Create(body) };
        request.Headers.TryAddWithoutValidation("api-key", options.Value.ApiKey);
        request.Headers.TryAddWithoutValidation("Idempotency-Key", message.MessageId.ToString());
        try
        {
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);
                return EmailProviderResult.Success(json.TryGetProperty("messageId", out var id) ? id.GetString() : null, (int)response.StatusCode);
            }
            var retry = response.Headers.RetryAfter?.Delta;
            var category = response.StatusCode switch
            {
                HttpStatusCode.RequestTimeout => Domain.Enums.EmailFailureCategory.Transient,
                HttpStatusCode.TooManyRequests => Domain.Enums.EmailFailureCategory.RateLimited,
                HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => Domain.Enums.EmailFailureCategory.Configuration,
                >= HttpStatusCode.InternalServerError => Domain.Enums.EmailFailureCategory.Transient,
                _ => Domain.Enums.EmailFailureCategory.Permanent
            };
            return EmailProviderResult.Failure(category, $"Brevo.Http{(int)response.StatusCode}",
                category == Domain.Enums.EmailFailureCategory.Configuration ? "Email provider configuration was rejected." : "Email provider rejected the request.",
                (int)response.StatusCode, retry);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        { return EmailProviderResult.Failure(Domain.Enums.EmailFailureCategory.Transient, "Brevo.Timeout", "Email provider request timed out."); }
        catch (JsonException)
        { return EmailProviderResult.Failure(Domain.Enums.EmailFailureCategory.Transient, "Brevo.InvalidResponse", "Email provider returned an invalid response."); }
        catch (HttpRequestException)
        { return EmailProviderResult.Failure(Domain.Enums.EmailFailureCategory.Transient, "Brevo.Network", "Email provider could not be reached."); }
    }
}
