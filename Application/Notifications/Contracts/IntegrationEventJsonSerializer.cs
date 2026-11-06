using System.Text.Json;

namespace Application.Notifications.Contracts;

public sealed class IntegrationEventJsonSerializer
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);
    public string Serialize<T>(T value) => JsonSerializer.Serialize(value, Options);
    public bool TryDeserializeEmailRequested(string payload, out EmailRequestedV1? value, out string error)
    {
        value = null;

        try
        {
            value = JsonSerializer.Deserialize<EmailRequestedV1>(payload, Options);
            error = value is null ? "Empty contract." : string.Empty;
            return value is not null;
        }
        catch (JsonException)
        {
            error = "Malformed JSON contract.";
            return false;
        }
    }
}
