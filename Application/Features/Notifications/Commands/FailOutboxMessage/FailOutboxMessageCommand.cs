using Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Notifications.Commands.FailOutboxMessage;

public sealed record FailOutboxMessageCommand(Guid Id, string SafeError, DateTimeOffset NextAttemptAt) : IRequest;

public sealed class FailOutboxMessageCommandHandler(IApplicationDbContext db)
    : IRequestHandler<FailOutboxMessageCommand>
{
    public async Task Handle(FailOutboxMessageCommand request, CancellationToken cancellationToken)
    {
        var row = await db.OutboxMessages.SingleAsync(x => x.Id == request.Id, cancellationToken);
        row.AttemptCount++;
        row.LastError = request.SafeError.Length > 500 ? request.SafeError[..500] : request.SafeError;
        row.NextAttemptAt = request.NextAttemptAt;
        await db.SaveChangesAsync(cancellationToken);
    }
}
