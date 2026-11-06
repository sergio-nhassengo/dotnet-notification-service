using Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Notifications.Commands.CompleteOutboxMessage;

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
