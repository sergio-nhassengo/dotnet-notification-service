using System.Threading;
using System.Threading.Tasks;
using Application.Common.Interfaces;
using Domain.Entities;
using MediatR;

namespace Application.Features.TodoLists.Commands.CreateTodoList;

public record CreateTodoListCommand(string Title) : IRequest<int>;

public class CreateTodoListCommandHandler(IApplicationDbContext context) : IRequestHandler<CreateTodoListCommand, int>
{
    public async Task<int> Handle(CreateTodoListCommand request, CancellationToken cancellationToken)
    {
        var entity = new TodoList { Title = request.Title };

        context.TodoLists.Add(entity);

        await context.SaveChangesAsync(cancellationToken);

        return entity.Id;
    }
}
