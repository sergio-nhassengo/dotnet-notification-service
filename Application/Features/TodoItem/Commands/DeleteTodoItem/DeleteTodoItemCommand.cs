using Application.Common.Interfaces;
using Domain.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.TodoItem.Commands.DeleteTodoItem;

public record DeleteTodoItemCommand(int Id) : IRequest;

public class DeleteTodoItemCommandHandler(IApplicationDbContext context) : IRequestHandler<DeleteTodoItemCommand>
{
    public async Task Handle(DeleteTodoItemCommand request, CancellationToken cancellationToken)
    {
        var entity = await context.TodoItems
            .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);

        if (entity is null)
        {
            throw new NotFoundException(nameof(Domain.Entities.TodoItem), request.Id);
        }

        context.TodoItems.Remove(entity);

        await context.SaveChangesAsync(cancellationToken);
    }
}
