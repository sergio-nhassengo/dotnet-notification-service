using System.Threading;
using System.Threading.Tasks;
using Application.Common.Interfaces;
using Domain.Common;
using Domain.Entities;
using MediatR;

namespace Application.Features.TodoLists.Commands.CreateTodoList;

public record CreateTodoListCommand(string Title) : IRequest<Result<int>>;

public class CreateTodoListCommandHandler(IApplicationDbContext context) : IRequestHandler<CreateTodoListCommand, Result<int>>
{
    public async Task<Result<int>> Handle(CreateTodoListCommand request, CancellationToken cancellationToken)
    {
        var entity = new TodoList { Title = request.Title };

        context.TodoLists.Add(entity);

        await context.SaveChangesAsync(cancellationToken);

        return entity.Id;
    }
}
