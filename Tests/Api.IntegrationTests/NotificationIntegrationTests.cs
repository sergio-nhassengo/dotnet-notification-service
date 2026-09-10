using System.Net;
using Confluent.Kafka;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Application.Features.Notifications.Commands.CreateEmail;
using Infrastructure.Notifications.Workers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Persistence;

namespace Api.IntegrationTests;

public sealed class NotificationIntegrationTests
{
    [Fact]
    public async Task Applications_have_isolated_idempotency_status_and_search_and_cannot_replay()
    {
        await using var factory = new NotificationSqliteFactory();
        var otc = factory.Client("otc-api"); var tan = factory.Client("tan-webservice");
        var request = Raw("same-key");
        var first = await otc.PostAsJsonAsync("/api/v1/notifications/email", request);
        Assert.Equal(HttpStatusCode.Accepted, first.StatusCode);
        var notification = (await first.Content.ReadFromJsonAsync<EmailAcceptedResponse>())!;
        Assert.Equal("Queued", notification.Status);
        var retry = await otc.PostAsJsonAsync("/api/v1/notifications/email", request);
        Assert.Equal(notification.NotificationId, (await retry.Content.ReadFromJsonAsync<EmailAcceptedResponse>())!.NotificationId);
        var conflict = await otc.PostAsJsonAsync("/api/v1/notifications/email", request with { Subject = "Changed" });
        Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);
        var other = await tan.PostAsJsonAsync("/api/v1/notifications/email", request);
        Assert.Equal(HttpStatusCode.Accepted, other.StatusCode);
        Assert.NotEqual(notification.NotificationId, (await other.Content.ReadFromJsonAsync<EmailAcceptedResponse>())!.NotificationId);
        Assert.Equal(HttpStatusCode.NotFound, (await tan.GetAsync($"/api/v1/notifications/{notification.NotificationId}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await otc.PostAsync($"/api/v1/notifications/{notification.NotificationId}/replay", null)).StatusCode);
        var search = await otc.GetFromJsonAsync<JsonElement>("/api/v1/notifications?businessReference=TAN-42");
        Assert.Equal(1, search.GetArrayLength());
        Assert.Equal("otc-api", search[0].GetProperty("applicationId").GetString());
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Equal(0, await db.OutboxMessages.CountAsync());
        Assert.Equal(2, await db.EmailNotifications.CountAsync());
    }

    [Fact]
    public async Task Attachments_are_durable_and_cannot_be_used_by_another_application()
    {
        await using var factory = new NotificationSqliteFactory();
        var otc = factory.Client("otc-api"); var tan = factory.Client("tan-webservice");
        using var form = new MultipartFormDataContent();
        var bytes = new ByteArrayContent(Encoding.UTF8.GetBytes("%PDF-test"));
        bytes.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        form.Add(bytes, "file", "ticket.pdf");
        var upload = await otc.PostAsync("/api/v1/notifications/attachments", form);
        Assert.Equal(HttpStatusCode.Created, upload.StatusCode);
        var id = (await upload.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("attachmentId").GetGuid();
        var request = Raw("attachment-key") with { Attachments = [new(id)] };
        Assert.Equal(HttpStatusCode.BadRequest, (await tan.PostAsJsonAsync("/api/v1/notifications/email", request)).StatusCode);
        Assert.Equal(HttpStatusCode.Accepted, (await otc.PostAsJsonAsync("/api/v1/notifications/email", request)).StatusCode);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Equal("%PDF-test", Encoding.UTF8.GetString((await db.NotificationAttachments.SingleAsync()).Content));
        var notification = await db.EmailNotifications.SingleAsync();
        Assert.Contains(id.ToString(), notification.ContentJson);
        Assert.Equal(2, JsonSerializer.Deserialize<Application.Notifications.Integration.EmailContent>(notification.ContentJson!)!.To.Count);
    }

    [Fact]
    public async Task Invalid_content_templates_and_credentials_are_rejected_before_acceptance()
    {
        await using var factory = new NotificationSqliteFactory();
        var client = factory.Client("otc-api");
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/v1/notifications/email", Raw("bad") with { Subject = "Header\r\nInjected" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/v1/notifications/email", Raw("bad") with { To = null! })).StatusCode);
        var template = new CreateEmailNotificationCommand("template", "c", new("a@example.com", null), "payment-confirmed",
            new Dictionary<string, string>(), null, null);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/v1/notifications/email", template)).StatusCode);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("ApiKey", "otc-api.wrong");
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/v1/notifications/email", Raw("bad"))).StatusCode);
        using var scope = factory.Services.CreateScope();
        Assert.Equal(0, await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().EmailNotifications.CountAsync());
    }

    [Fact]
    public async Task Kafka_mode_persists_outbox_with_scoped_content()
    {
        await using var factory = new NotificationSqliteFactory(useKafka: true);
        var response = await factory.Client("otc-api").PostAsJsonAsync("/api/v1/notifications/email", Raw("kafka"));
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var outbox = await db.OutboxMessages.SingleAsync();
        Assert.Equal(1, outbox.SchemaVersion);
        Assert.Equal("1", JsonNode.Parse(outbox.Headers)!["schema-version"]!.GetValue<string>());
        using var payload = JsonDocument.Parse(outbox.Payload);
        Assert.Equal("otc-api", payload.RootElement.GetProperty("applicationId").GetString());
        Assert.Equal(2, payload.RootElement.GetProperty("content").GetProperty("to").GetArrayLength());
        Assert.Equal("Pending", (await db.EmailNotifications.SingleAsync()).Status.ToString());
        var payloadText = outbox.Payload;
        var messageId = outbox.MessageKey;
        db.OutboxMessages.Remove(outbox);
        await db.SaveChangesAsync(); // Consumption remains valid after outbox retention cleanup.
        var processor = factory.Services.GetRequiredService<KafkaEmailMessageProcessor>();
        var inbound = Inbound(messageId, payloadText, "1");
        await processor.ProcessAsync(inbound, default);
        await processor.ProcessAsync(inbound, default);
        db.ChangeTracker.Clear();
        Assert.Equal("Queued", (await db.EmailNotifications.SingleAsync()).Status.ToString());
        Assert.Equal(1, await db.InboxMessages.CountAsync());
        Assert.Equal(0, await db.OutboxMessages.CountAsync());

        using var worker = ActivatorUtilities.CreateInstance<EmailDeliveryWorker>(factory.Services);
        var processNext = typeof(EmailDeliveryWorker).GetMethod("ProcessNext", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
        Assert.True(await (Task<bool>)processNext.Invoke(worker, [CancellationToken.None])!);
        db.ChangeTracker.Clear();
        Assert.Equal("Sent", (await db.EmailNotifications.SingleAsync()).Status.ToString());
        Assert.Equal(1, await db.DeliveryAttempts.CountAsync());
    }

    [Fact]
    public async Task Kafka_rejects_untrusted_scoped_content_and_null_contract_fields_without_creating_notifications()
    {
        await using var factory = new NotificationSqliteFactory();
        _ = factory.Client("otc-api");
        var processor = factory.Services.GetRequiredService<KafkaEmailMessageProcessor>();
        var email = new Application.Notifications.Contracts.EmailRequestedV1(Guid.NewGuid(), "c", "i", "a@example.com", null,
            "payment-confirmed", new Dictionary<string, string> { ["customerName"] = "A", ["paymentReference"] = "P" }, null, DateTimeOffset.UtcNow, null);
        var forged = email with
        {
            ApplicationId = "otc-api", RequestHash = "forged",
            Content = new Application.Notifications.Integration.EmailContent { To = [new("a@example.com")] }
        };
        var serializer = new Application.Common.Messaging.IntegrationEventJsonSerializer();
        await processor.ProcessAsync(Inbound(email.MessageId, serializer.Serialize(forged), "1"), default);
        await processor.ProcessAsync(Inbound(Guid.NewGuid(), serializer.Serialize(forged), "2", 2), default);
        await processor.ProcessAsync(Inbound(Guid.NewGuid(), "{\"contractVersion\":1,\"variables\":null}", "1", 1), default);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Equal(0, await db.EmailNotifications.CountAsync());
        Assert.Equal(3, await db.InboxMessages.CountAsync());
        Assert.Equal(3, await db.OutboxMessages.CountAsync());
    }

    [Fact]
    public async Task Admin_replay_revalidates_content_and_records_audit()
    {
        await using var factory = new NotificationSqliteFactory();
        var client = factory.Client("otc-api");
        var response = await client.PostAsJsonAsync("/api/v1/notifications/email", Raw("replay"));
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        var accepted = (await response.Content.ReadFromJsonAsync<EmailAcceptedResponse>())!;
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var entity = await db.EmailNotifications.SingleAsync();
        entity.MarkPermanentlyFailed("test", "Test failure", DateTimeOffset.UtcNow);
        await db.SaveChangesAsync();
        var generator = scope.ServiceProvider.GetRequiredService<Application.Common.Security.IJwtTokenGenerator>();
        var admin = new Domain.Entities.User { Id = 1, Email = "admin@example.com", UserName = "admin", FirstName = "A", LastName = "B",
            RoleId = 1, Role = new Domain.Entities.Role { Id = 1, Name = "Admin" } };
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", generator.GenerateToken(admin).Token);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsync($"/api/v1/notifications/{accepted.NotificationId}/replay", null)).StatusCode);
        db.ChangeTracker.Clear();
        Assert.Equal("RetryScheduled", (await db.EmailNotifications.SingleAsync()).Status.ToString());
        Assert.Equal(1, await db.NotificationReplays.CountAsync());
    }

    [Fact]
    public async Task Removing_template_version_preserves_existing_hashes_and_queued_envelopes()
    {
        await using var factory = new NotificationSqliteFactory(useKafka: true);
        var client = factory.Client("otc-api");
        var request = Raw("pre-version-removal");
        Assert.Equal(HttpStatusCode.Accepted, (await client.PostAsJsonAsync("/api/v1/notifications/email", request)).StatusCode);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var notification = await db.EmailNotifications.SingleAsync();
        var outbox = await db.OutboxMessages.SingleAsync();
        var serializer = new Application.Common.Messaging.IntegrationEventJsonSerializer();
        var legacyHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(serializer.Serialize(new
        {
            request.BusinessReference, request.CorrelationId, TemplateId = request.TemplateId ?? "", TemplateVersion = 0,
            Variables = Array.Empty<KeyValuePair<string, string>>(), request.Subject,
            ScheduledAt = request.ScheduledAt?.ToUniversalTime(), Content = request.Content()
        }))));
        var envelope = JsonNode.Parse(outbox.Payload)!;
        Assert.Null(envelope["templateVersion"]);
        envelope["requestHash"] = legacyHash;
        envelope["templateVersion"] = 0; // Simulate a previously persisted message.
        outbox.Payload = envelope.ToJsonString();
        notification.SetIntegrationContext(notification.ApplicationId, notification.BusinessReference, legacyHash, notification.ContentJson);
        await db.SaveChangesAsync();
        Assert.Equal(HttpStatusCode.Accepted, (await client.PostAsJsonAsync("/api/v1/notifications/email", request)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync("/api/v1/notifications/email", request with { Subject = "Different content" })).StatusCode);
        await factory.Services.GetRequiredService<KafkaEmailMessageProcessor>().ProcessAsync(Inbound(outbox.MessageKey, outbox.Payload, "1"), default);
        db.ChangeTracker.Clear();
        Assert.Equal("Queued", (await db.EmailNotifications.SingleAsync()).Status.ToString());
        Assert.Null(db.Model.FindEntityType(typeof(Domain.Entities.EmailNotification))!.FindProperty("TemplateVersion"));
    }

    private static ConsumeResult<string, string> Inbound(Guid messageId, string payload, string schema, long offset = 0) => new()
    {
        Topic = "primary-topic-notification", Partition = new Partition(0), Offset = new Offset(offset),
        Message = new Message<string, string>
        {
            Key = messageId.ToString(), Value = payload,
            Headers = new Headers { new Header("schema-version", Encoding.UTF8.GetBytes(schema)) }
        }
    };

    private static CreateEmailNotificationCommand Raw(string key) => new(key, "correlation", null, null, null, "TIP issued", null)
    {
        BusinessReference = "TAN-42", HtmlBody = "<p>Truck <b>42</b></p>", TextBody = "Truck 42",
        To = [new("one@example.com"), new("two@example.com")], Cc = [new("cc@example.com")]
    };
}

internal sealed class NotificationSqliteFactory(bool useKafka = false) : WebApplicationFactory<Program>
{
    private readonly SqliteConnection connection = new("Data Source=:memory:");
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        connection.Open();
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            var values = new Dictionary<string, string?>
            {
                ["Database:Provider"] = "Sqlite", ["ConnectionStrings:Default"] = "Data Source=:memory:",
                ["ConnectionStrings:Logging"] = "", ["Kafka:Enabled"] = useKafka.ToString(),
                ["EmailProvider:Provider"] = "Fake", ["OpenTelemetry:Otlp:Endpoint"] = "",
                ["Jwt:SigningKey"] = "test-only-notification-signing-key-at-least-32-characters"
            };
            foreach (var app in new[] { "otc-api", "weighbridge-api", "tan-webservice" })
                values[$"NotificationIntegration:Applications:{app}:ApiKeySha256"] = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes("test-secret")));
            configuration.AddInMemoryCollection(values);
        });
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<ApplicationDbContext>();
            services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<ApplicationDbContext>>();
            services.AddDbContext<ApplicationDbContext>(options => options.UseSqlite(connection));
            var workers = new[] { typeof(EmailDeliveryWorker), typeof(OutboxPublisherWorker), typeof(KafkaEmailConsumerWorker), typeof(OutboxCleanupWorker) };
            foreach (var descriptor in services.Where(x => x.ServiceType == typeof(IHostedService) && workers.Contains(x.ImplementationType)).ToArray())
                services.Remove(descriptor);
        });
    }
    public HttpClient Client(string app)
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("ApiKey", app + ".test-secret");
        return client;
    }
    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync(); await connection.DisposeAsync();
    }
}
