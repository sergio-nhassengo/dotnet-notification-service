using Application.Features.TodoLists.Commands.CreateTodoList;
using Application.UnitTests.Common;

namespace Application.UnitTests.Features.TodoLists.Commands.CreateTodoList;

public class CreateTodoListCommandHandlerTests
{
    [Fact]
    public async Task Handle_persists_a_TodoList_and_returns_its_generated_id()
    {
        await using var context = TestApplicationDbContextFactory.Create();
        var handler = new CreateTodoListCommandHandler(context);

        var result = await handler.Handle(new CreateTodoListCommand("Groceries"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value > 0);
        var persisted = await context.TodoLists.FindAsync(result.Value);
        Assert.NotNull(persisted);
        Assert.Equal("Groceries", persisted!.Title);
    }
}
