using System;
using Domain.Common;

namespace Domain.Entities;

public class User : BaseAuditableEntity<int>
{
    public string Email { get; set; } = string.Empty;

    public string FirstName { get; set; } = string.Empty;

    public string LastName { get; set; } = string.Empty;

    public string UserName { get; set; } = string.Empty;

    public string? MobilePhone { get; set; }

    public string PasswordHash { get; set; } = string.Empty;

    public DateTimeOffset? LastLogin { get; set; }

    public int RoleId { get; set; }

    public Role Role { get; set; } = null!;
}
