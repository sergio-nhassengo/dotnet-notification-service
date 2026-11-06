using Application.Notifications.Abstractions;
using Domain.Entities;

namespace Application.Notifications.Contracts;

public static class DeadLetterOutbox
{
    /// <summary>Creates the outbox row that publishes <paramref name="deadLetter"/> to the dead-letter topic.</summary>
    public static OutboxMessage Create(EmailDeadLetteredV1 deadLetter, IntegrationEventJsonSerializer serializer,
        INotificationDefaults defaults, DateTimeOffset now) => new()
    {
        Id = Guid.NewGuid(),
        MessageKey = deadLetter.OriginalMessageId,
        Topic = defaults.DeadLetterTopic,
        Payload = serializer.Serialize(deadLetter),
        Headers = serializer.Serialize(new Dictionary<string, string>
        {
            ["correlation-id"] = deadLetter.CorrelationId,
            ["causation-id"] = deadLetter.OriginalMessageId.ToString()
        }),
        OccurredAt = now,
        NextAttemptAt = now,
        CreatedAt = now
    };
}
