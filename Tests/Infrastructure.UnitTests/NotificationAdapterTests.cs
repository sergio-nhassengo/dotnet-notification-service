using Application.Notifications.Contracts;
using Application.Notifications.Models;
using Domain.Enums;
using Infrastructure.Notifications;
using Infrastructure.Notifications.Providers;

namespace Infrastructure.UnitTests;

public class NotificationAdapterTests
{
    [Fact]
    public void Serializer_round_trips_versioned_contract()
    {
        var serializer = new JsonIntegrationEventSerializer();
        var source = new EmailRequestedV1(Guid.NewGuid(), "c", "i", "a@example.com", null, "payment-confirmed", 1,
            new Dictionary<string, string> { { "name", "Ada" } }, null, "Normal", DateTimeOffset.UtcNow, null);
        Assert.True(serializer.TryDeserializeEmailRequested(serializer.Serialize(source), 1, out var result, out _));
        Assert.Equal(source.MessageId, result!.MessageId); Assert.Equal(1, result.ContractVersion);
    }

    [Fact]
    public void Serializer_rejects_unsupported_schema_without_exposing_payload()
    {
        var serializer = new JsonIntegrationEventSerializer();
        Assert.False(serializer.TryDeserializeEmailRequested("{personal-data}", 99, out _, out var error));
        Assert.Equal("Unsupported schema version.", error);
    }

    [Fact]
    public async Task Fake_provider_returns_stable_provider_id()
    {
        var id = Guid.NewGuid(); var provider = new FakeEmailProvider();
        var result = await provider.SendAsync(new EmailMessage(id, "a@example.com", null, "b@example.com", null, null, "s", "h", "t"), default);
        Assert.True(result.IsSuccess); Assert.Equal(EmailFailureCategory.None, result.FailureCategory); Assert.Contains(id.ToString("N"), result.ProviderMessageId);
    }
}
