using Application.Common.Interfaces;
using Domain.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.TodoLists.Commands.DeleteTodoList;

public record DeleteTodoListCommand(int Id) : IRequest;

public class DeleteTodoListCommandHandler(IApplicationDbContext context) : IRequestHandler<DeleteTodoListCommand>
{
    public async Task Handle(DeleteTodoListCommand request, CancellationToken cancellationToken)
    {
        var entity = await context.TodoLists
            .FirstOrDefaultAsync(l => l.Id == request.Id, cancellationToken);

        if (entity is null)
        {
            throw new NotFoundException(nameof(Domain.Entities.TodoList), request.Id);
        }

        context.TodoLists.Remove(entity);

        await context.SaveChangesAsync(cancellationToken);
    }
}
