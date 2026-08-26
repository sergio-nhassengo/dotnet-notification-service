using Application.Features.TodoLists.Commands.DeleteTodoList;
using Application.UnitTests.Common;
using Domain.Entities;
using Domain.Exceptions;

namespace Application.UnitTests.Features.TodoLists.Commands.DeleteTodoList;

public class DeleteTodoListCommandHandlerTests
{
    [Fact]
    public async Task Handle_removes_an_existing_list()
    {
        await using var context = TestApplicationDbContextFactory.Create();
        var entity = new TodoList { Title = "Groceries" };
        context.TodoLists.Add(entity);
        await context.SaveChangesAsync(CancellationToken.None);

        var handler = new DeleteTodoListCommandHandler(context);
        await handler.Handle(new DeleteTodoListCommand(entity.Id), CancellationToken.None);

        Assert.Null(await context.TodoLists.FindAsync(entity.Id));
    }

    [Fact]
    public async Task Handle_throws_NotFoundException_when_the_list_does_not_exist()
    {
        await using var context = TestApplicationDbContextFactory.Create();
        var handler = new DeleteTodoListCommandHandler(context);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            handler.Handle(new DeleteTodoListCommand(999), CancellationToken.None));
    }
}
