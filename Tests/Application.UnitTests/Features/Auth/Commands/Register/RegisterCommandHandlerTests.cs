using Application.Features.Auth.Commands.Register;
using Application.UnitTests.Common;
using Domain.Common;
using Domain.Constants;
using Domain.Entities;

namespace Application.UnitTests.Features.Auth.Commands.Register;

public class RegisterCommandHandlerTests
{
    private static RegisterCommand ValidCommand(string email = "new@example.com", string userName = "newuser") =>
        new(email, "New", "User", userName, null, "password123");

    [Fact]
    public async Task Handle_persists_a_user_with_the_default_role_and_a_hashed_password()
    {
        await using var context = TestApplicationDbContextFactory.Create();
        context.Roles.Add(new Role { Name = RoleNames.User });
        await context.SaveChangesAsync(CancellationToken.None);

        var hasher = new FakePasswordHasher();
        var handler = new RegisterCommandHandler(context, hasher);

        var result = await handler.Handle(ValidCommand(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var persisted = await context.Users.FindAsync(result.Value);
        Assert.NotNull(persisted);
        Assert.Equal("new@example.com", persisted!.Email);
        Assert.Equal(hasher.Hash("password123"), persisted.PasswordHash);

        var role = await context.Roles.FindAsync(persisted.RoleId);
        Assert.Equal(RoleNames.User, role!.Name);
    }

    [Fact]
    public async Task Handle_returns_a_Conflict_failure_for_a_duplicate_email()
    {
        await using var context = TestApplicationDbContextFactory.Create();
        context.Roles.Add(new Role { Name = RoleNames.User });
        var hasher = new FakePasswordHasher();
        context.Users.Add(new User
        {
            Email = "new@example.com",
            UserName = "someoneelse",
            FirstName = "A",
            LastName = "B",
            PasswordHash = hasher.Hash("whatever"),
            Role = new Role { Name = RoleNames.User }
        });
        await context.SaveChangesAsync(CancellationToken.None);

        var handler = new RegisterCommandHandler(context, hasher);

        var result = await handler.Handle(ValidCommand(email: "new@example.com", userName: "newuser"), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Conflict, result.Error.Type);
    }

    [Fact]
    public async Task Handle_returns_a_Conflict_failure_for_a_duplicate_username()
    {
        await using var context = TestApplicationDbContextFactory.Create();
        var hasher = new FakePasswordHasher();
        context.Users.Add(new User
        {
            Email = "someoneelse@example.com",
            UserName = "newuser",
            FirstName = "A",
            LastName = "B",
            PasswordHash = hasher.Hash("whatever"),
            Role = new Role { Name = RoleNames.User }
        });
        await context.SaveChangesAsync(CancellationToken.None);

        var handler = new RegisterCommandHandler(context, hasher);

        var result = await handler.Handle(ValidCommand(email: "new@example.com", userName: "newuser"), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Conflict, result.Error.Type);
    }

    [Fact]
    public async Task Handle_returns_a_NotFound_failure_when_the_default_role_is_missing()
    {
        await using var context = TestApplicationDbContextFactory.Create();
        var hasher = new FakePasswordHasher();
        var handler = new RegisterCommandHandler(context, hasher);

        var result = await handler.Handle(ValidCommand(), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.NotFound, result.Error.Type);
    }
}
