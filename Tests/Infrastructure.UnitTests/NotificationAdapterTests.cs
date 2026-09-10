using Application.Common.Messaging;
using Application.Notifications.Contracts;
using Application.Notifications.Models;
using Domain.Enums;
using Infrastructure.Notifications.Providers;

namespace Infrastructure.UnitTests;

public class NotificationAdapterTests
{
    [Fact]
    public void Serializer_round_trips_versioned_contract()
    {
        var serializer = new IntegrationEventJsonSerializer();
        var source = new EmailRequestedV1(Guid.NewGuid(), "c", "i", "a@example.com", null, "payment-confirmed",
            new Dictionary<string, string> { { "name", "Ada" } }, null, DateTimeOffset.UtcNow, null);
        Assert.True(serializer.TryDeserializeEmailRequested(serializer.Serialize(source), 1, out var result, out _));
        Assert.Equal(source.MessageId, result!.MessageId); Assert.Equal(1, result.ContractVersion);
        Assert.Equal(source.RecipientEmail, result.RecipientEmail); Assert.Equal(source.TemplateId, result.TemplateId);
        Assert.Equal(source.ScheduledAt, result.ScheduledAt);
    }

    [Fact]
    public void Serializer_rejects_unsupported_schema_without_exposing_payload()
    {
        var serializer = new IntegrationEventJsonSerializer();
        Assert.False(serializer.TryDeserializeEmailRequested("{personal-data}", 99, out _, out var error));
        Assert.Equal("Unsupported schema version.", error);
    }

    [Fact]
    public void Serializer_rejects_malformed_json_without_exposing_payload()
    {
        var serializer = new IntegrationEventJsonSerializer();

        Assert.False(serializer.TryDeserializeEmailRequested("{personal-data}", 1, out var result, out var error));
        Assert.Null(result);
        Assert.Equal("Malformed JSON contract.", error);
    }

    [Fact]
    public void Serializer_ignores_unknown_legacy_priority_property()
    {
        const string payload = """
            {"messageId":"42cf9b46-8298-4ec9-80cb-3654a538bf76","correlationId":"c","idempotencyKey":"i","recipientEmail":"a@example.com","recipientName":null,"templateId":"payment-confirmed","templateVersion":1,"variables":{},"subject":null,"priority":"High","requestedAt":"2026-09-04T08:00:00Z","scheduledAt":null,"contractVersion":1}
            """;
        var serializer = new IntegrationEventJsonSerializer();

        Assert.True(serializer.TryDeserializeEmailRequested(payload, 1, out var result, out _));
        Assert.Equal(Guid.Parse("42cf9b46-8298-4ec9-80cb-3654a538bf76"), result!.MessageId);
    }

    [Fact]
    public async Task Fake_provider_returns_stable_provider_id()
    {
        var id = Guid.NewGuid(); var provider = new FakeEmailProvider();
        var result = await provider.SendAsync(new EmailMessage(id, "a@example.com", null, "b@example.com", null, null, "s", "h", "t"), default);
        Assert.True(result.IsSuccess); Assert.Equal(EmailFailureCategory.None, result.FailureCategory); Assert.Contains(id.ToString("N"), result.ProviderMessageId);
    }
}
