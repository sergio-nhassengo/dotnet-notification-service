using Application.Notifications.Integration;

namespace Application.Notifications.Contracts;

public sealed record EmailRequestedV1(
    Guid MessageId,
    string CorrelationId,
    string IdempotencyKey,
    string RecipientEmail,
    string? RecipientName,
    string TemplateId,

    IReadOnlyDictionary<string, string> Variables,
    string? Subject,
    DateTimeOffset RequestedAt,
    DateTimeOffset? ScheduledAt)
{
    public string? ApplicationId { get; init; }
    public string? BusinessReference { get; init; }
    public string? RequestHash { get; init; }
    public EmailContent? Content { get; init; }

    public const int SchemaVersion = 1;
    public const string EventType = "email.requested";
    public int ContractVersion { get; init; } = SchemaVersion;
}

public sealed record EmailDeadLetteredV1(
    Guid OriginalMessageId,
    string CorrelationId,
    string OriginalTopic,
    int? OriginalPartition,
    long? OriginalOffset,
    string FailureCategory,
    string ErrorCode,
    string SafeErrorMessage,
    int AttemptCount,
    DateTimeOffset FailedAt,
    Guid NotificationId)
{
    public const int SchemaVersion = 1;
    public const string EventType = "email.dead-lettered";
    public int ContractVersion { get; init; } = SchemaVersion;
}
