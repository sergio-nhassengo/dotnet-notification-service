using Application.Common.Interfaces;
using Domain.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.TodoItem.Commands.UpdateTodoItem;

public record UpdateTodoItemCommand(int Id, int TodoListId, string Title, bool Done) : IRequest<Result>;

public class UpdateTodoItemCommandHandler(IApplicationDbContext context) : IRequestHandler<UpdateTodoItemCommand, Result>
{
    public async Task<Result> Handle(UpdateTodoItemCommand request, CancellationToken cancellationToken)
    {
        var entity = await context.TodoItems
            .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);

        if (entity is null)
        {
            return Result.Failure(Error.EntityNotFound(nameof(Domain.Entities.TodoItem), request.Id));
        }

        entity.TodoListId = request.TodoListId;
        entity.Title = request.Title;
        entity.Done = request.Done;

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
