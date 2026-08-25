using System.Collections.Generic;
using Domain.Common;

namespace Domain.Entities;

public class Role : BaseAuditableEntity<int>
{
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public IList<User> Users { get; private set; } = [];
}
