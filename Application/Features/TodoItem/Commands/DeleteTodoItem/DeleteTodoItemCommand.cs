using Application.Common.Interfaces;
using Domain.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.TodoItem.Commands.DeleteTodoItem;

public record DeleteTodoItemCommand(int Id) : IRequest<Result>;

public class DeleteTodoItemCommandHandler(IApplicationDbContext context) : IRequestHandler<DeleteTodoItemCommand, Result>
{
    public async Task<Result> Handle(DeleteTodoItemCommand request, CancellationToken cancellationToken)
    {
        var entity = await context.TodoItems
            .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);

        if (entity is null)
        {
            return Result.Failure(Error.EntityNotFound(nameof(Domain.Entities.TodoItem), request.Id));
        }

        context.TodoItems.Remove(entity);

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
