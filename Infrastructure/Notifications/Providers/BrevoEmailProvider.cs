using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Application.Notifications.Interfaces;
using Application.Notifications.Models;
using Infrastructure.Notifications.Options;
using Microsoft.Extensions.Options;

namespace Infrastructure.Notifications.Providers;

public sealed class BrevoEmailProvider(HttpClient client, IOptions<EmailProviderOptions> options) : IEmailProvider
{
    public string Name => "Brevo";
    public async Task<EmailProviderResult> SendAsync(EmailMessage message, CancellationToken ct)
    {
        var body = new
        {
            sender = new { email = message.SenderEmail, name = message.SenderName },
            to = new[] { new { email = message.RecipientEmail, name = message.RecipientName } },
            replyTo = message.ReplyTo is null ? null : new { email = message.ReplyTo },
            subject = message.Subject,
            htmlContent = message.HtmlBody,
            textContent = message.TextBody,
            headers = new Dictionary<string, string> { ["X-Mailin-custom"] = $"message-id:{message.MessageId}" }
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
        catch (HttpRequestException)
        { return EmailProviderResult.Failure(Domain.Enums.EmailFailureCategory.Transient, "Brevo.Network", "Email provider could not be reached."); }
    }
}
