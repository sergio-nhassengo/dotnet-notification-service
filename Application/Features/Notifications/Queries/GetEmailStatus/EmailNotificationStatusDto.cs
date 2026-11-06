using Domain.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Notifications.Queries.GetEmailStatus;

public sealed record EmailNotificationStatusDto(Guid NotificationId, Guid MessageId, string CorrelationId,
    string Status, int AttemptCount, DateTimeOffset CreatedAt, DateTimeOffset? LastAttemptAt,
    DateTimeOffset? SentAt, DateTimeOffset? FailedAt,
    string? LastErrorCode, string? LastErrorMessage, DateTimeOffset? NextAttemptAt)
{
    public string ApplicationId { get; init; } = "legacy";
    public string DeliveryMeaning { get; init; } = "Sent means accepted by the configured provider, not confirmed inbox delivery.";
}
