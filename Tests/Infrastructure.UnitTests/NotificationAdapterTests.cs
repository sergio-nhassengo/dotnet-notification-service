using Application.Notifications.Contracts;

namespace Infrastructure.UnitTests;

public class NotificationAdapterTests
{
    [Fact]
    public void Serializer_round_trips_contract()
    {
        var serializer = new IntegrationEventJsonSerializer();
        var source = new EmailRequestedV1(Guid.NewGuid(), "c", "i", "payment-confirmed",
            new Dictionary<string, string> { { "name", "Ada" } }, null, DateTimeOffset.UtcNow)
        {
            Content = new Application.Notifications.Models.EmailContent
            {
                To = [new("one@example.com", "One"), new("two@example.com", "Two")]
            }
        };
        var payload = serializer.Serialize(source);
        using var json = System.Text.Json.JsonDocument.Parse(payload);
        foreach (var removed in new[] { "recipientEmail", "recipientName", "businessReference" })
            Assert.False(json.RootElement.TryGetProperty(removed, out _));
        Assert.True(serializer.TryDeserializeEmailRequested(payload, out var result, out _));
        Assert.Equal(source.Content.To, result!.Content!.To);
        Assert.Equal(source.MessageId, result!.MessageId);
        Assert.Equal(source.TemplateId, result.TemplateId);
    }


    [Fact]
    public void Serializer_rejects_malformed_json_without_exposing_payload()
    {
        var serializer = new IntegrationEventJsonSerializer();

        Assert.False(serializer.TryDeserializeEmailRequested("{personal-data}", out var result, out var error));
        Assert.Null(result);
        Assert.Equal("Malformed JSON contract.", error);
    }


}
