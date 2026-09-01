using Application.Features.TodoItem.Queries.GetTodoItems;
using Application.Common.Interfaces;
using AutoMapper;
using AutoMapper.QueryableExtensions;
using Domain.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.TodoItem.Queries.GetTodoItemById;

public record GetTodoItemByIdQuery(int Id) : IRequest<Result<TodoItemDto>>;

public class GetTodoItemByIdQueryHandler(IApplicationDbContext context, IConfigurationProvider mapperConfiguration)
    : IRequestHandler<GetTodoItemByIdQuery, Result<TodoItemDto>>
{
    public async Task<Result<TodoItemDto>> Handle(GetTodoItemByIdQuery request, CancellationToken cancellationToken)
    {
        var result = await context.TodoItems
            .Where(x => x.Id == request.Id)
            .ProjectTo<TodoItemDto>(mapperConfiguration)
            .FirstOrDefaultAsync(cancellationToken);

        return result is null
            ? Result.Failure<TodoItemDto>(Error.EntityNotFound(nameof(Domain.Entities.TodoItem), request.Id))
            : result;
    }
}
