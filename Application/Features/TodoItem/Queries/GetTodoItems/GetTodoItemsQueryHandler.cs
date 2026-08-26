using Application.Common.Interfaces;
using AutoMapper;
using AutoMapper.QueryableExtensions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.TodoItem.Queries.GetTodoItems;

public class GetTodoItemsQueryHandler(IApplicationDbContext context, IConfigurationProvider mapperConfiguration)
    : IRequestHandler<GetTodoItemsQuery, List<TodoItemDto>>
{
    public Task<List<TodoItemDto>> Handle(GetTodoItemsQuery request, CancellationToken cancellationToken)
    {
        return context.TodoItems
            .OrderBy(x => x.TodoListId)
            .ProjectTo<TodoItemDto>(mapperConfiguration)
            .ToListAsync(cancellationToken);
    }
}
