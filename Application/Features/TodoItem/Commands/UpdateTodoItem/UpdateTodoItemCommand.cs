using Application.Common.Interfaces;
using Domain.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.TodoItem.Commands.UpdateTodoItem;

public record UpdateTodoItemCommand(int Id, int TodoListId, string Title, bool Done) : IRequest;

public class UpdateTodoItemCommandHandler(IApplicationDbContext context) : IRequestHandler<UpdateTodoItemCommand>
{
    public async Task Handle(UpdateTodoItemCommand request, CancellationToken cancellationToken)
    {
        var entity = await context.TodoItems
            .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);

        if (entity is null)
        {
            throw new NotFoundException(nameof(Domain.Entities.TodoItem), request.Id);
        }

        entity.TodoListId = request.TodoListId;
        entity.Title = request.Title;
        entity.Done = request.Done;

        await context.SaveChangesAsync(cancellationToken);
    }
}
