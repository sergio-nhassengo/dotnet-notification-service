using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Net;
using System.Text.Json;
using Application.Notifications.Abstractions;
using Application.Notifications.Models;
using Domain.Enums;
using Infrastructure.Notifications.Options;
using Microsoft.Extensions.Options;

namespace Infrastructure.Notifications.Providers;

public sealed class SmtpRelayEmailProvider(HttpClient client, IOptions<SmtpRelayOptions> options) : IEmailProvider
{
    public string Name => "SmtpRelay";

    public async Task<EmailProviderResult> SendAsync(EmailMessage message, CancellationToken ct)
    {
        var settings = options.Value;
        if (!settings.IsValid())
            return EmailProviderResult.Failure(EmailFailureCategory.Configuration, "SmtpRelay.Configuration", "SMTP relay configuration is invalid.");
        try
        {
            using var login = await client.PostAsJsonAsync("login", new { username = settings.Username, password = settings.Password }, ct);
            if (!login.IsSuccessStatusCode) return Failure(login, "Login");
            using var json = await JsonDocument.ParseAsync(await login.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
            var token = json.RootElement;
            foreach (var segment in settings.TokenProperty.Split('.'))
            {
                if (token.ValueKind != JsonValueKind.Object || !token.TryGetProperty(segment, out token))
                    return InvalidToken();
            }
            if (token.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(token.GetString()) ||
                token.GetString()!.Any(char.IsWhiteSpace)) return InvalidToken();

            using var request = new HttpRequestMessage(HttpMethod.Post, "SMTPRelay")
            {
                Content = JsonContent.Create(new
                {
                    from = message.SenderEmail,
                    to = message.To.Count > 0 ? string.Join(",", message.To.Select(x => x.Email)) : message.RecipientEmail,
                    cc = string.Join(",", message.Cc.Select(x => x.Email)),
                    subject = message.Subject,
                    text = message.TextBody,
                    html = message.HtmlBody,
                    createdBy = settings.CreatedBy,
                    // The relay contract requires an attachments array; this service never sends attachments.
                    attachments = Array.Empty<object>()
                })
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.GetString());
            using var response = await client.SendAsync(request, ct);
            return response.IsSuccessStatusCode ? EmailProviderResult.Success((int)response.StatusCode) : Failure(response, "Send");
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        { return EmailProviderResult.Failure(EmailFailureCategory.Transient, "SmtpRelay.Timeout", "SMTP relay request timed out."); }
        catch (HttpRequestException)
        { return EmailProviderResult.Failure(EmailFailureCategory.Transient, "SmtpRelay.Network", "SMTP relay could not be reached."); }
        catch (JsonException)
        { return InvalidToken(); }
    }

    private static EmailProviderResult InvalidToken() => EmailProviderResult.Failure(
        EmailFailureCategory.Configuration, "SmtpRelay.InvalidToken", "SMTP relay login did not return a valid token at the configured property.");

    private static EmailProviderResult Failure(HttpResponseMessage response, string stage)
    {
        var category = response.StatusCode switch
        {
            HttpStatusCode.RequestTimeout => EmailFailureCategory.Transient,
            HttpStatusCode.TooManyRequests => EmailFailureCategory.RateLimited,
            HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => EmailFailureCategory.Configuration,
            >= HttpStatusCode.InternalServerError => EmailFailureCategory.Transient,
            _ => EmailFailureCategory.Permanent
        };
        var retryAfter = response.Headers.RetryAfter?.Delta;
        if (retryAfter is null && response.Headers.RetryAfter?.Date is { } date)
            retryAfter = date > DateTimeOffset.UtcNow ? date - DateTimeOffset.UtcNow : TimeSpan.Zero;
        return EmailProviderResult.Failure(category, $"SmtpRelay.{stage}.Http{(int)response.StatusCode}",
            "SMTP relay rejected the request.", (int)response.StatusCode, retryAfter);
    }
}
