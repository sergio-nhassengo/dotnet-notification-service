using Application.Features.Auth.Commands.Login;
using Application.UnitTests.Common;
using Domain.Common;
using Domain.Entities;

namespace Application.UnitTests.Features.Auth.Commands.Login;

public class LoginCommandHandlerTests
{
    private static async Task<(Persistence.ApplicationDbContext Context, User User)> SeedUser(
        Persistence.ApplicationDbContext context, FakePasswordHasher hasher, string password = "correct horse")
    {
        var role = new Role { Name = "Admin" };
        context.Roles.Add(role);

        var user = new User
        {
            Email = "jane@example.com",
            UserName = "jane",
            FirstName = "Jane",
            LastName = "Doe",
            PasswordHash = hasher.Hash(password),
            Role = role
        };
        context.Users.Add(user);
        await context.SaveChangesAsync(CancellationToken.None);

        return (context, user);
    }

    [Fact]
    public async Task Handle_returns_a_LoginResponse_and_updates_LastLogin_on_success()
    {
        var clock = new FakeDateTime(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
        await using var context = TestApplicationDbContextFactory.Create(clock);
        var hasher = new FakePasswordHasher();
        var (_, user) = await SeedUser(context, hasher);

        var jwtGenerator = new FakeJwtTokenGenerator { TokenToReturn = "the-token" };
        var handler = new LoginCommandHandler(context, hasher, jwtGenerator, clock);

        var result = await handler.Handle(new LoginCommand("jane", "correct horse"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("the-token", result.Value.AccessToken);
        Assert.Equal("jane", result.Value.UserName);
        Assert.Equal("jane@example.com", result.Value.Email);
        Assert.Equal("Admin", result.Value.Role);
        Assert.Same(user, jwtGenerator.LastUser);

        var persisted = await context.Users.FindAsync(user.Id);
        Assert.Equal(clock.Now, persisted!.LastLogin);
    }

    [Fact]
    public async Task Handle_returns_an_Unauthorized_failure_for_an_unknown_username()
    {
        await using var context = TestApplicationDbContextFactory.Create();
        var hasher = new FakePasswordHasher();
        await SeedUser(context, hasher);

        var handler = new LoginCommandHandler(context, hasher, new FakeJwtTokenGenerator(), new FakeDateTime(DateTimeOffset.UtcNow));

        var result = await handler.Handle(new LoginCommand("unknown", "correct horse"), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Unauthorized, result.Error.Type);
    }

    [Fact]
    public async Task Handle_returns_an_Unauthorized_failure_for_a_wrong_password()
    {
        await using var context = TestApplicationDbContextFactory.Create();
        var hasher = new FakePasswordHasher();
        await SeedUser(context, hasher);

        var handler = new LoginCommandHandler(context, hasher, new FakeJwtTokenGenerator(), new FakeDateTime(DateTimeOffset.UtcNow));

        var result = await handler.Handle(new LoginCommand("jane", "wrong password"), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Unauthorized, result.Error.Type);
    }
}
