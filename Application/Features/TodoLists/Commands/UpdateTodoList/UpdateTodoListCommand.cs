using System.Threading;
using System.Threading.Tasks;
using Application.Common.Interfaces;
using Domain.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.TodoLists.Commands.UpdateTodoList;

public record UpdateTodoListCommand(int Id, string Title) : IRequest;

public class UpdateTodoListCommandHandler(IApplicationDbContext context) : IRequestHandler<UpdateTodoListCommand>
{
    public async Task Handle(UpdateTodoListCommand request, CancellationToken cancellationToken)
    {
        var entity = await context.TodoLists
            .FirstOrDefaultAsync(l => l.Id == request.Id, cancellationToken);

        if (entity is null)
        {
            throw new NotFoundException(nameof(Domain.Entities.TodoList), request.Id);
        }

        entity.Title = request.Title;

        await context.SaveChangesAsync(cancellationToken);
    }
}
