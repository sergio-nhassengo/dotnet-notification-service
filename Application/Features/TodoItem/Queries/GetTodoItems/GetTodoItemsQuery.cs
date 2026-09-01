using Application.Common.Interfaces;
using AutoMapper;
using AutoMapper.QueryableExtensions;
using Domain.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.TodoItem.Queries.GetTodoItems;

public record GetTodoItemsQuery : IRequest<Result<List<TodoItemDto>>>;

public class GetTodoItemsQueryHandler(IApplicationDbContext context, IConfigurationProvider mapperConfiguration)
    : IRequestHandler<GetTodoItemsQuery, Result<List<TodoItemDto>>>
{
    public async Task<Result<List<TodoItemDto>>> Handle(GetTodoItemsQuery request, CancellationToken cancellationToken)
    {
        var result = await context.TodoItems
            .OrderBy(x => x.TodoListId)
            .ProjectTo<TodoItemDto>(mapperConfiguration)
            .ToListAsync(cancellationToken);

        return result;
    }
}
