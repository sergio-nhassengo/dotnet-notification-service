using System.Threading;
using System.Threading.Tasks;
using Application.Common.Interfaces;
using Domain.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.TodoLists.Commands.UpdateTodoList;

public record UpdateTodoListCommand(int Id, string Title) : IRequest<Result>;

public class UpdateTodoListCommandHandler(IApplicationDbContext context) : IRequestHandler<UpdateTodoListCommand, Result>
{
    public async Task<Result> Handle(UpdateTodoListCommand request, CancellationToken cancellationToken)
    {
        var entity = await context.TodoLists
            .FirstOrDefaultAsync(l => l.Id == request.Id, cancellationToken);

        if (entity is null)
        {
            return Result.Failure(Error.EntityNotFound(nameof(Domain.Entities.TodoList), request.Id));
        }

        entity.Title = request.Title;

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
