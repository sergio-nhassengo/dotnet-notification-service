using System.Threading;
using System.Threading.Tasks;
using Application.Common.Interfaces;
using Domain.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.TodoLists.Commands.DeleteTodoList;

public record DeleteTodoListCommand(int Id) : IRequest<Result>;

public class DeleteTodoListCommandHandler(IApplicationDbContext context) : IRequestHandler<DeleteTodoListCommand, Result>
{
    public async Task<Result> Handle(DeleteTodoListCommand request, CancellationToken cancellationToken)
    {
        var entity = await context.TodoLists
            .FirstOrDefaultAsync(l => l.Id == request.Id, cancellationToken);

        if (entity is null)
        {
            return Result.Failure(Error.EntityNotFound(nameof(Domain.Entities.TodoList), request.Id));
        }

        context.TodoLists.Remove(entity);

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
