using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using Application.Common.Security;
using Microsoft.AspNetCore.Http;

namespace Infrastructure.Services.Auth;

public class CurrentUserService(IHttpContextAccessor httpContextAccessor) : ICurrentUserService
{
    private ClaimsPrincipal? User => httpContextAccessor.HttpContext?.User;

    public string? UserId => User?.FindFirstValue(ClaimTypes.NameIdentifier);

    public IReadOnlyList<string> Roles =>
        User?.FindAll(ClaimTypes.Role).Select(c => c.Value).ToArray() ?? [];

    public bool IsInRole(string role) => User?.IsInRole(role) ?? false;
}
