using Application.Common.Security;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace Persistence.UnitTests;

public class ApplicationDbContextAuditTests
{
    private static ApplicationDbContext CreateContext(FakeDateTime dateTime, string? userId)
    {
        var currentUserService = Substitute.For<ICurrentUserService>();
        currentUserService.UserId.Returns(userId);

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new ApplicationDbContext(options, dateTime, currentUserService);
    }

    private static User CreateUser(string userName) => new()
    {
        Email = $"{userName}@example.com",
        FirstName = "Jane",
        LastName = "Doe",
        UserName = userName,
        PasswordHash = "hashed-password",
        Role = new Role { Name = $"Role-{userName}" }
    };

    [Fact]
    public async Task Adding_an_entity_stamps_Created_and_LastModified_with_the_current_user_and_time()
    {
        var clock = new FakeDateTime(new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero));
        await using var context = CreateContext(clock, "42");

        var entity = CreateUser("jane.doe");
        context.Users.Add(entity);
        await context.SaveChangesAsync(CancellationToken.None);

        Assert.Equal(clock.Now, entity.Created);
        Assert.Equal("42", entity.CreatedBy);
        Assert.Equal(clock.Now, entity.LastModified);
        Assert.Equal("42", entity.LastModifiedBy);
    }

    [Fact]
    public async Task Adding_an_entity_with_no_current_user_falls_back_to_anonymous()
    {
        var clock = new FakeDateTime(DateTimeOffset.UtcNow);
        await using var context = CreateContext(clock, userId: null);

        var entity = CreateUser("jane.doe");
        context.Users.Add(entity);
        await context.SaveChangesAsync(CancellationToken.None);

        Assert.Equal("anonymous", entity.CreatedBy);
        Assert.Equal("anonymous", entity.LastModifiedBy);
    }

    [Fact]
    public async Task Modifying_an_entity_advances_LastModified_but_leaves_Created_unchanged()
    {
        var clock = new FakeDateTime(new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero));
        await using var context = CreateContext(clock, "42");

        var entity = CreateUser("jane.doe");
        context.Users.Add(entity);
        await context.SaveChangesAsync(CancellationToken.None);

        var originalCreated = entity.Created;
        var originalCreatedBy = entity.CreatedBy;

        clock.Now = clock.Now.AddHours(2);
        entity.LastName = "Smith";
        await context.SaveChangesAsync(CancellationToken.None);

        Assert.Equal(originalCreated, entity.Created);
        Assert.Equal(originalCreatedBy, entity.CreatedBy);
        Assert.Equal(clock.Now, entity.LastModified);
        Assert.Equal("42", entity.LastModifiedBy);
    }

    [Fact]
    public async Task Removing_a_User_deletes_it()
    {
        var clock = new FakeDateTime(DateTimeOffset.UtcNow);
        await using var context = CreateContext(clock, "42");

        var entity = CreateUser("jane.doe");
        context.Users.Add(entity);
        await context.SaveChangesAsync(CancellationToken.None);

        context.Users.Remove(entity);
        await context.SaveChangesAsync(CancellationToken.None);

        Assert.False(await context.Users.AnyAsync(u => u.Id == entity.Id, CancellationToken.None));
    }
}
