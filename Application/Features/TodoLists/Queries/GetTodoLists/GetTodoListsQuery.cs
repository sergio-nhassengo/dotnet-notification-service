using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Application.Common.Interfaces;
using AutoMapper;
using AutoMapper.QueryableExtensions;
using Domain.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Application.Features.TodoLists.Queries.GetTodoLists;

public record GetTodoListsQuery : IRequest<Result<List<TodoListDto>>>;

public class GetTodoListsQueryHandler(IApplicationDbContext context, ILogger<GetTodoListsQueryHandler> logger, IConfigurationProvider mapperConfiguration)
    : IRequestHandler<GetTodoListsQuery, Result<List<TodoListDto>>>
{
    public async Task<Result<List<TodoListDto>>> Handle(GetTodoListsQuery request, CancellationToken cancellationToken)
    {
        var result = await context.TodoLists
            .OrderBy(l => l.Title)
            .ProjectTo<TodoListDto>(mapperConfiguration)
            .ToListAsync(cancellationToken);

        logger.LogWarning("Executed query {@Query} with {@Result}", request, result);

        return result;
    }
}
