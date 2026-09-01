namespace Application.Notifications.Contracts;

public sealed record EmailRequestedV1(
    Guid MessageId,
    string CorrelationId,
    string IdempotencyKey,
    string RecipientEmail,
    string? RecipientName,
    string TemplateId,
    int TemplateVersion,
    IReadOnlyDictionary<string, string> Variables,
    string? Subject,
    string Priority,
    DateTimeOffset RequestedAt,
    DateTimeOffset? ScheduledAt)
{
    public const int SchemaVersion = 1;
    public const string EventType = "email.requested";
    public int ContractVersion { get; init; } = SchemaVersion;
}

public sealed record EmailDeadLetteredV1(
    Guid OriginalMessageId, string CorrelationId, string OriginalTopic, int? OriginalPartition,
    long? OriginalOffset, string FailureCategory, string ErrorCode, string SafeErrorMessage,
    int AttemptCount, DateTimeOffset FailedAt, Guid NotificationId)
{
    public const int SchemaVersion = 1;
    public const string EventType = "email.dead-lettered";
    public int ContractVersion { get; init; } = SchemaVersion;
}
