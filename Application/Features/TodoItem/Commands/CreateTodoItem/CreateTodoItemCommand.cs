using Application.Common.Interfaces;
using Domain.Common;
using Domain.Entities;
using MediatR;

namespace Application.Features.TodoItem.Commands.CreateTodoItem;

public record CreateTodoItemCommand(int TodoListId, string Title, bool Done) : IRequest<Result<int>>;

public class CreateTodoItemCommandHandler(IApplicationDbContext context) : IRequestHandler<CreateTodoItemCommand, Result<int>>
{
    public async Task<Result<int>> Handle(CreateTodoItemCommand request, CancellationToken cancellationToken)
    {
        var entity = new Domain.Entities.TodoItem
        {
            TodoListId = request.TodoListId,
            Title = request.Title,
            Done = request.Done
        };

        context.TodoItems.Add(entity);

        await context.SaveChangesAsync(cancellationToken);

        return entity.Id;
    }
}
