using System.Net;
using System.Text.Json;
using Application.Notifications.Models;
using Domain.Enums;
using Infrastructure.Notifications.Options;
using Infrastructure.Notifications.Providers;
using Microsoft.Extensions.Options;

namespace Infrastructure.UnitTests;

public sealed class SmtpRelayEmailProviderTests
{
    private static EmailMessage Message() => new(Guid.NewGuid(), "recipient@example.com", null,
        "tansystem@portmaputo.com", null, "Test Email", "<p>Test</p>", "Test")
        { Cc = [new("cc@example.com")] };

    private static SmtpRelayEmailProvider Provider(HttpMessageHandler handler, string? tokenProperty = null) =>
        new(new HttpClient(handler) { BaseAddress = new Uri("https://relay.invalid/api/") },
            Options.Create(new SmtpRelayOptions { Username = "John.doe", Password = "test-password", CreatedBy = "user@example.com", TokenProperty = tokenProperty ?? new SmtpRelayOptions().TokenProperty }));

    [Fact]
    public async Task Logs_in_and_sends_expected_payload_with_bearer_token()
    {
        var calls = 0;
        var handler = new Handler(async (request, ct) =>
        {
            using var payload = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(ct));
            var body = payload.RootElement;
            Assert.Equal(HttpMethod.Post, request.Method);
            if (++calls == 1)
            {
                Assert.Equal("/api/login", request.RequestUri!.AbsolutePath);
                Assert.Null(request.Headers.Authorization);
                Assert.Equal("John.doe", body.GetProperty("username").GetString());
                Assert.Equal("test-password", body.GetProperty("password").GetString());
                return Response(HttpStatusCode.OK, "{\"userId\":1620,\"accessToken\":\"test-token\",\"firstName\":\"Test\",\"role\":\"Administrator\"}");
            }
            Assert.Equal("/api/SMTPRelay", request.RequestUri!.AbsolutePath);
            Assert.Equal("Bearer test-token", request.Headers.Authorization!.ToString());
            Assert.Equal("tansystem@portmaputo.com", body.GetProperty("from").GetString());
            Assert.Equal("recipient@example.com", body.GetProperty("to").GetString());
            Assert.Equal("cc@example.com", body.GetProperty("cc").GetString());
            Assert.Equal("Test Email", body.GetProperty("subject").GetString());
            Assert.Equal("Test", body.GetProperty("text").GetString());
            Assert.Equal("<p>Test</p>", body.GetProperty("html").GetString());
            Assert.Equal("user@example.com", body.GetProperty("createdBy").GetString());
            Assert.Equal(0, body.GetProperty("attachments").GetArrayLength());
            return Response(HttpStatusCode.Accepted);
        });
        Assert.True((await Provider(handler).SendAsync(Message(), default)).IsSuccess);
        Assert.Equal(2, calls);
    }

    [Theory]
    [InlineData(401, EmailFailureCategory.Configuration)]
    [InlineData(429, EmailFailureCategory.RateLimited)]
    [InlineData(500, EmailFailureCategory.Transient)]
    [InlineData(400, EmailFailureCategory.Permanent)]
    public async Task Maps_relay_errors_without_exposing_response(int status, EmailFailureCategory expected)
    {
        var calls = 0;
        var handler = new Handler((_, _) => Task.FromResult(++calls == 1
            ? Response(HttpStatusCode.OK, "{\"accessToken\":\"abc\"}") : Response((HttpStatusCode)status, "sensitive")));
        var result = await Provider(handler).SendAsync(Message(), default);
        Assert.Equal(expected, result.FailureCategory);
        Assert.Equal(status, result.StatusCode);
        Assert.DoesNotContain("sensitive", result.SafeErrorMessage!);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("not-json")]
    [InlineData("{\"accessToken\":123}")]
    public async Task Invalid_login_response_never_sends_email(string body)
    {
        var calls = 0;
        var handler = new Handler((_, _) => { calls++; return Task.FromResult(Response(HttpStatusCode.OK, body)); });
        Assert.Equal("SmtpRelay.InvalidToken", (await Provider(handler).SendAsync(Message(), default)).ErrorCode);
        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task Failed_login_never_sends_email()
    {
        var calls = 0;
        var handler = new Handler((_, _) => { calls++; return Task.FromResult(Response(HttpStatusCode.Unauthorized)); });
        Assert.Equal(EmailFailureCategory.Configuration, (await Provider(handler).SendAsync(Message(), default)).FailureCategory);
        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task Attachments_are_rejected_before_login()
    {
        var handler = new Handler((_, _) => throw new InvalidOperationException("Unexpected HTTP request"));
        var result = await Provider(handler).SendAsync(Message() with { Attachments = [new("a.txt", "text/plain", [1], null)] }, default);
        Assert.Equal("SmtpRelay.AttachmentsUnsupported", result.ErrorCode);
    }

    [Fact]
    public async Task Caller_cancellation_is_propagated()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var handler = new Handler((_, ct) => Task.FromCanceled<HttpResponseMessage>(ct));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Provider(handler).SendAsync(Message(), cancellation.Token));
    }

    private static HttpResponseMessage Response(HttpStatusCode status, string body = "{}") => new(status) { Content = new StringContent(body) };
    private sealed class Handler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => send(request, cancellationToken);
    }
}
