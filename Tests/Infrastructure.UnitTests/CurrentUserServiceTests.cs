using System.Security.Claims;
using Infrastructure.Services.Auth;
using Microsoft.AspNetCore.Http;
using NSubstitute;

namespace Infrastructure.UnitTests;

public class CurrentUserServiceTests
{
    [Fact]
    public void When_there_is_no_HttpContext_UserId_is_null()
    {
        var accessor = Substitute.For<IHttpContextAccessor>();
        accessor.HttpContext.Returns((HttpContext?)null);

        var service = new CurrentUserService(accessor);

        Assert.Null(service.UserId);
    }

    [Fact]
    public void When_there_is_no_HttpContext_Roles_is_empty()
    {
        var accessor = Substitute.For<IHttpContextAccessor>();
        accessor.HttpContext.Returns((HttpContext?)null);

        var service = new CurrentUserService(accessor);

        Assert.Empty(service.Roles);
    }

    [Fact]
    public void When_there_is_no_HttpContext_IsInRole_is_false()
    {
        var accessor = Substitute.For<IHttpContextAccessor>();
        accessor.HttpContext.Returns((HttpContext?)null);

        var service = new CurrentUserService(accessor);

        Assert.False(service.IsInRole("Admin"));
    }

    private static IHttpContextAccessor BuildAccessorFor(string userId, params string[] roles)
    {
        var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, userId) };
        claims.AddRange(roles.Select(r => new Claim(ClaimTypes.Role, r)));

        var identity = new ClaimsIdentity(claims, "TestAuth");
        var httpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) };

        var accessor = Substitute.For<IHttpContextAccessor>();
        accessor.HttpContext.Returns(httpContext);

        return accessor;
    }

    [Fact]
    public void UserId_matches_the_NameIdentifier_claim()
    {
        var service = new CurrentUserService(BuildAccessorFor("42", "Admin"));

        Assert.Equal("42", service.UserId);
    }

    [Fact]
    public void Roles_contains_the_users_role_claims()
    {
        var service = new CurrentUserService(BuildAccessorFor("42", "Admin", "User"));

        Assert.Contains("Admin", service.Roles);
        Assert.Contains("User", service.Roles);
    }

    [Fact]
    public void IsInRole_is_true_for_a_role_the_user_has_and_false_otherwise()
    {
        var service = new CurrentUserService(BuildAccessorFor("42", "Admin"));

        Assert.True(service.IsInRole("Admin"));
        Assert.False(service.IsInRole("User"));
    }
}
