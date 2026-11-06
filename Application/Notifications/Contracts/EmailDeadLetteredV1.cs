
namespace Application.Notifications.Contracts;

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
}
