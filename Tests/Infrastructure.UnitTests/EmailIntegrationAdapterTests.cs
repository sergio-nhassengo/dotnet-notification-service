using System.Net;
using System.Text.Json;
using Application.Notifications.Integration;
using Application.Notifications.Models;
using Infrastructure.Notifications.Options;
using Infrastructure.Notifications.Providers;
using Microsoft.Extensions.Options;

namespace Infrastructure.UnitTests;

public sealed class EmailIntegrationAdapterTests
{
    [Fact]
    public void Smtp_message_preserves_recipients_cc_attachments_and_inline_logo()
    {
        using var message = SmtpEmailProvider.CreateMessage(Message() with
        {
            Attachments = [new("ticket.pdf", "application/pdf", [1, 2], null), new("logo.png", "image/png", [3], "logo")]
        });
        Assert.Equal(2, message.To.Count); Assert.Single(message.CC);
        Assert.Single(message.Attachments);
        Assert.Equal("ticket.pdf", message.Attachments[0].Name);
        Assert.Equal("logo", message.AlternateViews[1].LinkedResources[0].ContentId);
    }
    [Fact]
    public async Task Brevo_payload_preserves_all_recipients_cc_and_attachment_bytes()
    {
        var handler = new CaptureHandler();
        var provider = new BrevoEmailProvider(new HttpClient(handler) { BaseAddress = new Uri("https://provider.invalid/") }, Options.Create(new EmailProviderOptions()));
        var result = await provider.SendAsync(Message() with { Attachments = [new("ticket.pdf", "application/pdf", [1, 2], null)] }, default);
        Assert.True(result.IsSuccess);
        using var json = JsonDocument.Parse(handler.Payload!);
        Assert.True(json.RootElement.GetProperty("headers").TryGetProperty("Idempotency-Key", out _));
        Assert.Equal(2, json.RootElement.GetProperty("to").GetArrayLength());
        Assert.Equal("cc@example.com", json.RootElement.GetProperty("cc")[0].GetProperty("email").GetString());
        Assert.Equal("AQI=", json.RootElement.GetProperty("attachment")[0].GetProperty("content").GetString());
    }
    [Fact]
    public async Task Brevo_text_only_content_gets_an_encoded_html_alternative()
    {
        var handler = new CaptureHandler();
        var provider = new BrevoEmailProvider(new HttpClient(handler) { BaseAddress = new Uri("https://provider.invalid/") }, Options.Create(new EmailProviderOptions()));
        Assert.True((await provider.SendAsync(Message() with { HtmlBody = "", TextBody = "<ticket>" }, default)).IsSuccess);
        using var json = JsonDocument.Parse(handler.Payload!);
        Assert.Equal("<pre>&lt;ticket&gt;</pre>", json.RootElement.GetProperty("htmlContent").GetString());
    }

    private static EmailMessage Message() => new(Guid.NewGuid(), "a@example.com", null, "sender@example.com", "Sender", null, "Ticket", "<p>Ticket</p>", "Ticket")
    { To = [new("a@example.com"), new("b@example.com")], Cc = [new("cc@example.com")] };
    private sealed class CaptureHandler : HttpMessageHandler
    {
        public string? Payload { get; private set; }
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Payload = await request.Content!.ReadAsStringAsync(ct);
            return new HttpResponseMessage(HttpStatusCode.Created) { Content = new StringContent("{\"messageId\":\"provider-1\"}") };
        }
    }
}
