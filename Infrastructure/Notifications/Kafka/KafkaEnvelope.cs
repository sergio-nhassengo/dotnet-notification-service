namespace Infrastructure.Notifications.Kafka;

public sealed record KafkaEnvelope(Guid MessageKey, string Topic, string Payload,
    IReadOnlyDictionary<string, string> Headers);
