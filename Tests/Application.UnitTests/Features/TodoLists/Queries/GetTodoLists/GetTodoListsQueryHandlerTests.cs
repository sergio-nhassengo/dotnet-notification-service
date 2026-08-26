using Application.Features.TodoLists.Queries.GetTodoLists;
using Application.UnitTests.Common;
using Domain.Entities;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace Application.UnitTests.Features.TodoLists.Queries.GetTodoLists;

public class GetTodoListsQueryHandlerTests
{
    [Fact]
    public async Task Handle_returns_lists_ordered_by_title_with_correct_item_counts()
    {
        await using var context = TestApplicationDbContextFactory.Create();

        var zebra = new TodoList { Title = "Zebra" };
        var apple = new TodoList { Title = "Apple" };
        apple.Items.Add(new TodoItem { Title = "Item" });

        context.TodoLists.AddRange(zebra, apple);
        await context.SaveChangesAsync(CancellationToken.None);

        var logger = Substitute.For<ILogger<GetTodoListsQueryHandler>>();
        var handler = new GetTodoListsQueryHandler(context, logger, TestApplicationDbContextFactory.CreateMapperConfiguration());

        var result = await handler.Handle(new GetTodoListsQuery(), CancellationToken.None);

        Assert.Equal(["Apple", "Zebra"], result.Select(r => r.Title));
        Assert.Equal(1, result.Single(r => r.Title == "Apple").ItemCount);
        Assert.Equal(0, result.Single(r => r.Title == "Zebra").ItemCount);
    }
}
