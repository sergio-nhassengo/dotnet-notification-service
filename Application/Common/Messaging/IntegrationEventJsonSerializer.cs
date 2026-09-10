using System.Text.Json;
using Application.Notifications.Contracts;

namespace Application.Common.Messaging;

public sealed class IntegrationEventJsonSerializer
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);
    public string Serialize<T>(T value) => JsonSerializer.Serialize(value, Options);
    public bool TryDeserializeEmailRequested(string payload, int schemaVersion, out EmailRequestedV1? value, out string error)
    {
        value = null;
        if (schemaVersion != EmailRequestedV1.SchemaVersion)
        {
            error = "Unsupported schema version.";
            return false;
        }

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
