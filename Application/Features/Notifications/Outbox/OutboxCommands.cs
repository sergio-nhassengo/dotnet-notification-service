using System.Data;
using Application.Common.Interfaces;
using Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Notifications.Commands.Outbox;

public sealed record ClaimOutboxBatchCommand(string Owner, int BatchSize, DateTimeOffset Now, TimeSpan Lease)
    : IRequest<IReadOnlyList<OutboxMessage>>;

public sealed class ClaimOutboxBatchCommandHandler(IApplicationDbContext db, IApplicationTransaction transaction)
    : IRequestHandler<ClaimOutboxBatchCommand, IReadOnlyList<OutboxMessage>>
{
    public Task<IReadOnlyList<OutboxMessage>> Handle(ClaimOutboxBatchCommand request, CancellationToken cancellationToken) =>
        transaction.ExecuteAsync<IReadOnlyList<OutboxMessage>>(IsolationLevel.Serializable, async ct =>
        {
            var rows = await db.OutboxMessages
                .Where(x => x.ProcessedAt == null && x.NextAttemptAt <= request.Now &&
                            (x.LeaseExpiresAt == null || x.LeaseExpiresAt < request.Now))
                .OrderBy(x => x.OccurredAt).Take(request.BatchSize).ToListAsync(ct);
            foreach (var row in rows)
            {
                row.LeaseOwner = request.Owner;
                row.LeaseExpiresAt = request.Now.Add(request.Lease);
            }
            await db.SaveChangesAsync(ct);
            return rows;
        }, cancellationToken);
}

public sealed record CompleteOutboxMessageCommand(Guid Id, DateTimeOffset Now) : IRequest;

public sealed class CompleteOutboxMessageCommandHandler(IApplicationDbContext db)
    : IRequestHandler<CompleteOutboxMessageCommand>
{
    public async Task Handle(CompleteOutboxMessageCommand request, CancellationToken cancellationToken)
    {
        var row = await db.OutboxMessages.SingleAsync(x => x.Id == request.Id, cancellationToken);
        row.ProcessedAt = request.Now;
        row.LeaseOwner = null;
        row.LeaseExpiresAt = null;
        row.LastError = null;
        await db.SaveChangesAsync(cancellationToken);
    }
}

public sealed record FailOutboxMessageCommand(Guid Id, string SafeError, DateTimeOffset NextAttemptAt) : IRequest;

public sealed class FailOutboxMessageCommandHandler(IApplicationDbContext db)
    : IRequestHandler<FailOutboxMessageCommand>
{
    public async Task Handle(FailOutboxMessageCommand request, CancellationToken cancellationToken)
    {
        var row = await db.OutboxMessages.SingleAsync(x => x.Id == request.Id, cancellationToken);
        row.AttemptCount++;
        row.LastError = request.SafeError;
        row.NextAttemptAt = request.NextAttemptAt;
        row.LeaseOwner = null;
        row.LeaseExpiresAt = null;
        await db.SaveChangesAsync(cancellationToken);
    }
}

public sealed record CleanupProcessedOutboxCommand(DateTimeOffset Cutoff) : IRequest<int>;

public sealed class CleanupProcessedOutboxCommandHandler(IApplicationDbContext db)
    : IRequestHandler<CleanupProcessedOutboxCommand, int>
{
    public Task<int> Handle(CleanupProcessedOutboxCommand request, CancellationToken cancellationToken) =>
        db.OutboxMessages.Where(x => x.ProcessedAt < request.Cutoff).ExecuteDeleteAsync(cancellationToken);
}
