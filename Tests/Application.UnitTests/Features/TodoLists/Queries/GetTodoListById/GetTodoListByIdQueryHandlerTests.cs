using Application.Features.TodoLists.Queries.GetTodoListById;
using Application.UnitTests.Common;
using Domain.Common;
using Domain.Entities;

namespace Application.UnitTests.Features.TodoLists.Queries.GetTodoListById;

public class GetTodoListByIdQueryHandlerTests
{
    [Fact]
    public async Task Handle_returns_a_mapped_dto_for_an_existing_list()
    {
        await using var context = TestApplicationDbContextFactory.Create();
        var entity = new TodoList { Title = "Groceries" };
        entity.Items.Add(new TodoItem { Title = "Milk" });
        entity.Items.Add(new TodoItem { Title = "Eggs" });
        context.TodoLists.Add(entity);
        await context.SaveChangesAsync(CancellationToken.None);

        var handler = new GetTodoListByIdQueryHandler(context, TestApplicationDbContextFactory.CreateMapperConfiguration());
        var result = await handler.Handle(new GetTodoListByIdQuery(entity.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(entity.Id, result.Value.Id);
        Assert.Equal("Groceries", result.Value.Title);
        Assert.Equal(2, result.Value.ItemCount);
    }

    [Fact]
    public async Task Handle_returns_a_NotFound_failure_when_the_list_does_not_exist()
    {
        await using var context = TestApplicationDbContextFactory.Create();
        var handler = new GetTodoListByIdQueryHandler(context, TestApplicationDbContextFactory.CreateMapperConfiguration());

        var result = await handler.Handle(new GetTodoListByIdQuery(999), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.NotFound, result.Error.Type);
    }
}
