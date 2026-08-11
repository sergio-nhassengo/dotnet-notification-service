using AutoMapper;
using Domain.Entities;

namespace Application.Features.TodoLists.Queries.GetTodoLists;

public class TodoListDto
{
    public int Id { get; init; }

    public string Title { get; init; } = string.Empty;

    public int ItemCount { get; init; }

    private class Mapping : Profile
    {
        public Mapping()
        {
            CreateMap<TodoList, TodoListDto>()
                .ForMember(d => d.ItemCount, opt => opt.MapFrom(s => s.Items.Count));
        }
    }
}
