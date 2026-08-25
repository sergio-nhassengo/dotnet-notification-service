using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Application.Common.Interfaces;
using Application.Features.TodoLists.Queries.GetTodoLists;
using AutoMapper;
using AutoMapper.QueryableExtensions;
using Domain.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.TodoLists.Queries.GetTodoListById;

public record GetTodoListByIdQuery(int Id) : IRequest<TodoListDto>;

public class GetTodoListByIdQueryHandler(IApplicationDbContext context, IConfigurationProvider mapperConfiguration)
    : IRequestHandler<GetTodoListByIdQuery, TodoListDto>
{
    public async Task<TodoListDto> Handle(GetTodoListByIdQuery request, CancellationToken cancellationToken)
    {
        var result = await context.TodoLists
            .Where(l => l.Id == request.Id)
            .ProjectTo<TodoListDto>(mapperConfiguration)
            .FirstOrDefaultAsync(cancellationToken);

        return result ?? throw new NotFoundException(nameof(Domain.Entities.TodoList), request.Id);
    }
}
