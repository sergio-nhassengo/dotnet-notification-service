using Application.Common.Interfaces;
using Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Notifications.Queries.Outbox;

public sealed record GetPendingOutboxBatchQuery(int BatchSize, DateTimeOffset Now)
    : IRequest<IReadOnlyList<OutboxMessage>>;

public sealed class GetPendingOutboxBatchQueryHandler(IApplicationDbContext db)
    : IRequestHandler<GetPendingOutboxBatchQuery, IReadOnlyList<OutboxMessage>>
{
    public async Task<IReadOnlyList<OutboxMessage>> Handle(
        GetPendingOutboxBatchQuery request,
        CancellationToken cancellationToken)
    {
        return await db.OutboxMessages
            .AsNoTracking()
            .Where(x => x.ProcessedAt == null && x.NextAttemptAt <= request.Now)
            .OrderBy(x => x.OccurredAt)
            .Take(request.BatchSize)
            .ToListAsync(cancellationToken);
    }
}
