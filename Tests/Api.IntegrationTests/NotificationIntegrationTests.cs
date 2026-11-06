using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Net;
using System.Security.Cryptography;
using System.Text.Json.Nodes;
using System.Text.Json;
using System.Text;
using Application.Features.Notifications.Commands.CreateEmail;
using Confluent.Kafka;
using Infrastructure.Notifications.Kafka;
using Infrastructure.Notifications.Workers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NSubstitute;
using Persistence;

namespace Api.IntegrationTests;

public sealed class NotificationIntegrationTests
{
    [Fact]
    public async Task Applications_have_isolated_idempotency_status_and_search()
    {
        await using var factory = new NotificationSqliteFactory();
        var otc = factory.Client("otc-api"); var tan = factory.Client("tan-webservice");
        var request = Raw("same-key");
        var first = await otc.PostAsJsonAsync("/api/v1/notifications/email", request);
        Assert.Equal(HttpStatusCode.Accepted, first.StatusCode);
        var notification = (await first.Content.ReadFromJsonAsync<EmailAcceptedResponse>())!;
        Assert.Equal("Pending", notification.Status);
        var retry = await otc.PostAsJsonAsync("/api/v1/notifications/email", request);
        Assert.Equal(notification.NotificationId, (await retry.Content.ReadFromJsonAsync<EmailAcceptedResponse>())!.NotificationId);
        var conflict = await otc.PostAsJsonAsync("/api/v1/notifications/email", request with { Subject = "Changed" });
        Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);
        var other = await tan.PostAsJsonAsync("/api/v1/notifications/email", request);
        Assert.Equal(HttpStatusCode.Accepted, other.StatusCode);
        Assert.NotEqual(notification.NotificationId, (await other.Content.ReadFromJsonAsync<EmailAcceptedResponse>())!.NotificationId);
        Assert.Equal(HttpStatusCode.NotFound, (await tan.GetAsync($"/api/v1/notifications/{notification.NotificationId}")).StatusCode);
        var search = await otc.GetFromJsonAsync<JsonElement>("/api/v1/notifications?correlationId=correlation");
        Assert.Equal(1, search.GetArrayLength());
        Assert.Equal("otc-api", search[0].GetProperty("applicationId").GetString());
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Equal(2, await db.OutboxMessages.CountAsync());
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
        Assert.Equal(2, JsonSerializer.Deserialize<Application.Notifications.Models.EmailContent>(notification.ContentJson!)!.To.Count);
    }

    [Fact]
    public async Task Invalid_content_templates_and_credentials_are_rejected_before_acceptance()
    {
        await using var factory = new NotificationSqliteFactory();
        var client = factory.Client("otc-api");
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/v1/notifications/email", Raw("bad") with { Subject = "Header\r\nInjected" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/v1/notifications/email", Raw("bad") with { To = null! })).StatusCode);
        var template = new CreateEmailNotificationCommand("template", "c", new("a@example.com", null), "payment-confirmed",
            new Dictionary<string, string>(), null);
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
        using var payload = JsonDocument.Parse(outbox.Payload);
        Assert.Equal("otc-api", payload.RootElement.GetProperty("applicationId").GetString());
        Assert.Equal(2, payload.RootElement.GetProperty("content").GetProperty("to").GetArrayLength());
        Assert.Equal("Pending", (await db.EmailNotifications.SingleAsync()).Status.ToString());
        var payloadText = outbox.Payload;
        var messageId = outbox.MessageKey;
        db.OutboxMessages.Remove(outbox);
        await db.SaveChangesAsync(); // Consumption remains valid after outbox retention cleanup.
        var processor = factory.Services.GetRequiredService<KafkaEmailMessageProcessor>();
        var inbound = Inbound(messageId, payloadText);
        await processor.ProcessAsync(inbound, default);
        await processor.ProcessAsync(inbound, default);
        db.ChangeTracker.Clear();
        Assert.Equal("Queued", (await db.EmailNotifications.SingleAsync()).Status.ToString());
        Assert.Equal(0, await db.OutboxMessages.CountAsync());

        using var worker = ActivatorUtilities.CreateInstance<EmailDeliveryWorker>(factory.Services);
        var processNext = typeof(EmailDeliveryWorker).GetMethod("ProcessNext", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
        Assert.True(await (Task<bool>)processNext.Invoke(worker, [CancellationToken.None])!);
        db.ChangeTracker.Clear();
        Assert.Equal("Sent", (await db.EmailNotifications.SingleAsync()).Status.ToString());
    }

    [Fact]
    public async Task Kafka_rejects_untrusted_scoped_content_and_null_contract_fields_without_creating_notifications()
    {
        await using var factory = new NotificationSqliteFactory();
        _ = factory.Client("otc-api");
        var processor = factory.Services.GetRequiredService<KafkaEmailMessageProcessor>();
        var email = new Application.Notifications.Contracts.EmailRequestedV1(Guid.NewGuid(), "c", "i",
            "payment-confirmed", new Dictionary<string, string> { ["customerName"] = "A", ["paymentReference"] = "P" }, null, DateTimeOffset.UtcNow);
        var forged = email with
        {
            ApplicationId = "otc-api", RequestHash = "forged",
            Content = new Application.Notifications.Models.EmailContent { To = [new("a@example.com")] }
        };
        var serializer = new Application.Notifications.Contracts.IntegrationEventJsonSerializer();
        await processor.ProcessAsync(Inbound(email.MessageId, serializer.Serialize(forged)), default);
        await processor.ProcessAsync(Inbound(Guid.NewGuid(), serializer.Serialize(forged), 2), default);
        await processor.ProcessAsync(Inbound(Guid.NewGuid(), "{\"variables\":null}", 1), default);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Equal(0, await db.EmailNotifications.CountAsync());
        Assert.Equal(3, await db.OutboxMessages.CountAsync());
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
        var serializer = new Application.Notifications.Contracts.IntegrationEventJsonSerializer();
        var legacyHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(serializer.Serialize(new
        {
            request.CorrelationId, TemplateId = request.TemplateId ?? "", TemplateVersion = 0,
            Variables = Array.Empty<KeyValuePair<string, string>>(), request.Subject,
            Content = request.Content()
        }))));
        var envelope = JsonNode.Parse(outbox.Payload)!;
        Assert.Null(envelope["templateVersion"]);
        envelope["requestHash"] = legacyHash;
        envelope["templateVersion"] = 0; // Simulate a previously persisted message.
        outbox.Payload = envelope.ToJsonString();
        notification.SetIntegrationContext(notification.ApplicationId, legacyHash, notification.ContentJson);
        await db.SaveChangesAsync();
        Assert.Equal(HttpStatusCode.Accepted, (await client.PostAsJsonAsync("/api/v1/notifications/email", request)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync("/api/v1/notifications/email", request with { Subject = "Different content" })).StatusCode);
        await factory.Services.GetRequiredService<KafkaEmailMessageProcessor>().ProcessAsync(Inbound(outbox.MessageKey, outbox.Payload), default);
        db.ChangeTracker.Clear();
        Assert.Equal("Queued", (await db.EmailNotifications.SingleAsync()).Status.ToString());
        Assert.Null(db.Model.FindEntityType(typeof(Domain.Entities.EmailNotification))!.FindProperty("TemplateVersion"));
    }

    private static ConsumeResult<string, string> Inbound(Guid messageId, string payload, long offset = 0) => new()
    {
        Topic = "primary-topic-notification", Partition = new Partition(0), Offset = new Offset(offset),
        Message = new Message<string, string>
        {
            Key = messageId.ToString(), Value = payload,
            Headers = new Headers()
        }
    };

    [Fact]
    public async Task Authenticated_application_without_permission_receives_forbidden()
    {
        await using var factory = new NotificationSqliteFactory();
        var client = factory.Client("restricted-api");
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync("/api/v1/notifications/email", Raw("raw-denied"))).StatusCode);
        var template = new CreateEmailNotificationCommand("template-denied", "c", new("a@example.com", null), "tan-message",
            new Dictionary<string, string>(), null);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync("/api/v1/notifications/email", template)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/v1/notifications?applicationId=otc-api")).StatusCode);
        using var scope = factory.Services.CreateScope();
        Assert.Equal(0, await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().EmailNotifications.CountAsync());
    }

    private static CreateEmailNotificationCommand Raw(string key) => new(key, "correlation", null, null, null, "TIP issued")
    {
        HtmlBody = "<p>Truck <b>42</b></p>", TextBody = "Truck 42",
        To = [new("one@example.com"), new("two@example.com")], Cc = [new("cc@example.com")]
    };
}

internal sealed class NotificationSqliteFactory(bool useKafka = false, Microsoft.EntityFrameworkCore.Diagnostics.IInterceptor? interceptor = null) : WebApplicationFactory<Program>
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
                ["EmailProvider:Provider"] = "Smtp",
                ["Smtp:Host"] = "localhost",
            };
            foreach (var app in new[] { "otc-api", "weighbridge-api", "tan-webservice", "restricted-api" })
                values[$"NotificationIntegration:Applications:{app}:ApiKeySha256"] = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes("test-secret")));
            // Explicit test-only tenants keep isolation tests independent of deployed applications.
            foreach (var app in new[] { "otc-api", "weighbridge-api" })
            {
                var prefix = $"NotificationIntegration:Applications:{app}:";
                values[prefix + "SenderEmail"] = "test@example.com";
                values[prefix + "AllowRawContent"] = "true";
                values[prefix + "AllowSubjectOverride"] = "true";
                values[prefix + "AllowedTemplateIds:0"] = "payment-confirmed";
            }
            // Authenticated tenant without raw-content permission and with a single allowed template.
            values["NotificationIntegration:Applications:restricted-api:SenderEmail"] = "restricted@example.com";
            values["NotificationIntegration:Applications:restricted-api:AllowedTemplateIds:0"] = "payment-confirmed";
            configuration.AddInMemoryCollection(values);
        });
        builder.ConfigureServices(services =>
        {
            // Delivery tests substitute the external transport; no production no-op provider is registered.
            var transport = Substitute.For<Application.Notifications.Abstractions.IEmailProvider>();
            transport.Name.Returns("TestTransport");
            transport.SendAsync(Arg.Any<Application.Notifications.Models.EmailMessage>(), Arg.Any<CancellationToken>())
                .Returns(Task.FromResult(Application.Notifications.Models.EmailProviderResult.Success(202)));
            services.RemoveAll<Application.Notifications.Abstractions.IEmailProvider>();
            services.AddSingleton(transport);
            services.RemoveAll<ApplicationDbContext>();
            services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<ApplicationDbContext>>();
            services.AddDbContext<ApplicationDbContext>(options =>
            {
                options.UseSqlite(connection);
                if (interceptor is not null) options.AddInterceptors(interceptor);
            });
            var workers = new[] { typeof(EmailDeliveryWorker), typeof(OutboxPublisherWorker), typeof(KafkaEmailConsumerWorker), typeof(OutboxCleanupWorker), typeof(AttachmentCleanupWorker) };
            foreach (var descriptor in services.Where(x => x.ServiceType == typeof(IHostedService) && workers.Contains(x.ImplementationType)).ToArray())
                services.Remove(descriptor);
        });
    }
    public HttpClient Client(string app)
    {
        var client = CreateClient();
        using var scope = Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Database.EnsureCreated();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("ApiKey", app + ".test-secret");
        return client;
    }
    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync(); await connection.DisposeAsync();
    }
}
