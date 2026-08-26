using MediatR;

namespace Application.Features.TodoItem.Queries.GetTodoItems;

public record GetTodoItemsQuery : IRequest<List<TodoItemDto>>;
