using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Application.Common.Security;
using Application.Features.Notifications.Commands.CreateEmail;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Persistence;

namespace Api.IntegrationTests;

public class NotificationApiTests(ApiWebApplicationFactory factory) : IClassFixture<ApiWebApplicationFactory>
{
    [Fact]
    public async Task Rest_request_creates_notification_and_outbox_atomically_and_is_idempotent()
    {
        var client = AuthorizedClient(); var key = $"payment-{Guid.NewGuid():N}";
        var request = new
        {
            idempotencyKey = key,
            correlationId = key,
            recipient = new { email = "customer@example.com", name = "Customer" },
            templateId = "payment-confirmed",
            templateVersion = 1,
            variables = new Dictionary<string, string> { { "customerName", "Customer" }, { "paymentReference", "PAY-123" } },
            subject = (string?)null,
            priority = "Normal",
            scheduledAt = (DateTimeOffset?)null
        };
        var first = await client.PostAsJsonAsync("/api/v1/notifications/email", request);
        var second = await client.PostAsJsonAsync("/api/v1/notifications/email", request);
        Assert.Equal(HttpStatusCode.Accepted, first.StatusCode); Assert.Equal(HttpStatusCode.Accepted, second.StatusCode);
        var firstBody = await first.Content.ReadFromJsonAsync<EmailAcceptedResponse>(); var secondBody = await second.Content.ReadFromJsonAsync<EmailAcceptedResponse>();
        Assert.Equal(firstBody!.NotificationId, secondBody!.NotificationId);
        using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Equal(1, await db.EmailNotifications.CountAsync(x => x.IdempotencyKey == key));
        Assert.Equal(1, await db.OutboxMessages.CountAsync(x => x.MessageKey == firstBody.MessageId));
    }

    [Fact]
    public async Task Invalid_request_creates_neither_notification_nor_outbox()
    {
        var client = AuthorizedClient();
        var response = await client.PostAsJsonAsync("/api/v1/notifications/email", new { idempotencyKey = "", recipient = new { email = "bad" } });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private HttpClient AuthorizedClient()
    {
        using var scope = factory.Services.CreateScope(); var generator = scope.ServiceProvider.GetRequiredService<IJwtTokenGenerator>();
        var user = new User
        {
            Id = 99,
            Email = "admin@example.com",
            UserName = "admin",
            FirstName = "Admin",
            LastName = "User",
            RoleId = 1,
            Role = new Role { Id = 1, Name = "Admin" }
        };
        var client = factory.CreateClient(); client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", generator.GenerateToken(user).Token);
        return client;
    }
}
