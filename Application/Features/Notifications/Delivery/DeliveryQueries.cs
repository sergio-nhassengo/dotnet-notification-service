using Application.Common.Interfaces;
using Domain.Entities;
using Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Notifications.Queries.Delivery;

public sealed record GetDueEmailDeliveryBatchQuery(int BatchSize, DateTimeOffset Now)
    : IRequest<IReadOnlyList<EmailNotification>>;

public sealed class GetDueEmailDeliveryBatchQueryHandler(IApplicationDbContext db)
    : IRequestHandler<GetDueEmailDeliveryBatchQuery, IReadOnlyList<EmailNotification>>
{
    public async Task<IReadOnlyList<EmailNotification>> Handle(
        GetDueEmailDeliveryBatchQuery request,
        CancellationToken cancellationToken)
    {
        var eligibleStates = new[] { NotificationStatus.Queued, NotificationStatus.RetryScheduled };

        return await db.EmailNotifications
            .AsNoTracking()
            .Where(x => eligibleStates.Contains(x.Status) &&
                        x.NextAttemptAt.HasValue && x.NextAttemptAt.Value <= request.Now)
            .OrderByDescending(x => x.Priority)
            .ThenBy(x => x.NextAttemptAt)
            .Take(request.BatchSize)
            .ToListAsync(cancellationToken);
    }
}
