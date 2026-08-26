using Application.Features.TodoItem.Queries.GetTodoItems;
using Application.Common.Interfaces;
using AutoMapper;
using AutoMapper.QueryableExtensions;
using Domain.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.TodoItem.Queries.GetTodoItemById;

public record GetTodoItemByIdQuery(int Id) : IRequest<TodoItemDto>;

public class GetTodoItemByIdQueryHandler(IApplicationDbContext context, IConfigurationProvider mapperConfiguration)
    : IRequestHandler<GetTodoItemByIdQuery, TodoItemDto>
{
    public async Task<TodoItemDto> Handle(GetTodoItemByIdQuery request, CancellationToken cancellationToken)
    {
        var result = await context.TodoItems
            .Where(x => x.Id == request.Id)
            .ProjectTo<TodoItemDto>(mapperConfiguration)
            .FirstOrDefaultAsync(cancellationToken);

        return result ?? throw new NotFoundException(nameof(Domain.Entities.TodoItem), request.Id);
    }
}
