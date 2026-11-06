using Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Notifications.Commands.CleanupProcessedOutbox;

public sealed record CleanupProcessedOutboxCommand(DateTimeOffset Cutoff) : IRequest<int>;

public sealed class CleanupProcessedOutboxCommandHandler(IApplicationDbContext db)
    : IRequestHandler<CleanupProcessedOutboxCommand, int>
{
    public Task<int> Handle(CleanupProcessedOutboxCommand request, CancellationToken cancellationToken) =>
        db.OutboxMessages.Where(x => x.ProcessedAt < request.Cutoff).ExecuteDeleteAsync(cancellationToken);
}
