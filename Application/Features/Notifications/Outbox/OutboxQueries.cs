using Application.Common.Interfaces;
using Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Notifications.Queries.Outbox;

public sealed record GetNextPendingOutboxMessageQuery(DateTimeOffset Now)
    : IRequest<OutboxMessage?>;

public sealed class GetNextPendingOutboxMessageQueryHandler(IApplicationDbContext db)
    : IRequestHandler<GetNextPendingOutboxMessageQuery, OutboxMessage?>
{
    public Task<OutboxMessage?> Handle(
        GetNextPendingOutboxMessageQuery request,
        CancellationToken cancellationToken)
    {
        return db.OutboxMessages
            .AsNoTracking()
            .Where(x => x.ProcessedAt == null && x.NextAttemptAt <= request.Now)
            .OrderBy(x => x.OccurredAt)
            .FirstOrDefaultAsync(cancellationToken);
    }
}
