using Domain.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Notifications.Queries.FindEmails;

public sealed record NotificationSummary(Guid NotificationId, string ApplicationId,
    string CorrelationId, string Status, int AttemptCount, DateTimeOffset CreatedAt);
