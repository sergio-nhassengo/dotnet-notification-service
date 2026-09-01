using Application.Features.TodoLists.Commands.UpdateTodoList;
using Application.UnitTests.Common;
using Domain.Common;
using Domain.Entities;

namespace Application.UnitTests.Features.TodoLists.Commands.UpdateTodoList;

public class UpdateTodoListCommandHandlerTests
{
    [Fact]
    public async Task Handle_updates_the_title_of_an_existing_list()
    {
        await using var context = TestApplicationDbContextFactory.Create();
        var entity = new TodoList { Title = "Groceries" };
        context.TodoLists.Add(entity);
        await context.SaveChangesAsync(CancellationToken.None);

        var handler = new UpdateTodoListCommandHandler(context);
        var result = await handler.Handle(new UpdateTodoListCommand(entity.Id, "Groceries (updated)"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var persisted = await context.TodoLists.FindAsync(entity.Id);
        Assert.Equal("Groceries (updated)", persisted!.Title);
    }

    [Fact]
    public async Task Handle_returns_a_NotFound_failure_when_the_list_does_not_exist()
    {
        await using var context = TestApplicationDbContextFactory.Create();
        var handler = new UpdateTodoListCommandHandler(context);

        var result = await handler.Handle(new UpdateTodoListCommand(999, "Anything"), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.NotFound, result.Error.Type);
    }
}
