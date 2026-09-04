using Application.Common.Interfaces;
using Domain.Entities;
using Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Notifications.Queries.Delivery;

public sealed record GetNextDueEmailDeliveryQuery(DateTimeOffset Now)
    : IRequest<EmailNotification?>;

public sealed class GetNextDueEmailDeliveryQueryHandler(IApplicationDbContext db)
    : IRequestHandler<GetNextDueEmailDeliveryQuery, EmailNotification?>
{
    public Task<EmailNotification?> Handle(
        GetNextDueEmailDeliveryQuery request,
        CancellationToken cancellationToken)
    {
        var eligibleStates = new[] { NotificationStatus.Queued, NotificationStatus.RetryScheduled };

        return db.EmailNotifications
            .AsNoTracking()
            .Where(x => eligibleStates.Contains(x.Status) &&
                        x.NextAttemptAt.HasValue && x.NextAttemptAt.Value <= request.Now)
            .OrderByDescending(x => x.Priority)
            .ThenBy(x => x.NextAttemptAt)
            .FirstOrDefaultAsync(cancellationToken);
    }
}
