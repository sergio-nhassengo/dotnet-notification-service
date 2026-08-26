using AutoMapper;

namespace Application.Features.TodoItem.Queries.GetTodoItems;

public class TodoItemDto
{
    public int Id { get; init; }

    public int TodoListId { get; init; }

    public string Title { get; init; } = string.Empty;

    public bool Done { get; init; }

    private class Mapping : Profile
    {
        public Mapping()
        {
            CreateMap<Domain.Entities.TodoItem, TodoItemDto>();
        }
    }
}
