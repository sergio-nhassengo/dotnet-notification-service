using Application.Common.Interfaces;
using Domain.Entities;
using MediatR;

namespace Application.Features.TodoItem.Commands.CreateTodoItem;

public record CreateTodoItemCommand(int TodoListId, string Title, bool Done) : IRequest<int>;

public class CreateTodoItemCommandHandler(IApplicationDbContext context) : IRequestHandler<CreateTodoItemCommand, int>
{
    public async Task<int> Handle(CreateTodoItemCommand request, CancellationToken cancellationToken)
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
