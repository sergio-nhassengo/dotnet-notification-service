using Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Notifications.Commands.Outbox;

public sealed record CompleteOutboxMessageCommand(Guid Id, DateTimeOffset Now) : IRequest;

public sealed class CompleteOutboxMessageCommandHandler(IApplicationDbContext db)
    : IRequestHandler<CompleteOutboxMessageCommand>
{
    public async Task Handle(CompleteOutboxMessageCommand request, CancellationToken cancellationToken)
    {
        var row = await db.OutboxMessages.SingleAsync(x => x.Id == request.Id, cancellationToken);
        row.ProcessedAt = request.Now;
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
