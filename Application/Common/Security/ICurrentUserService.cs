using System.Collections.Generic;

namespace Application.Common.Security;

public interface ICurrentUserService
{
    string? UserId { get; }

    IReadOnlyList<string> Roles { get; }

    bool IsInRole(string role);
}
