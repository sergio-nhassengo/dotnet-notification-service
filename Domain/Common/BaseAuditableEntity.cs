using System;

namespace Domain.Common;

public abstract class BaseAuditableEntity<T> : BaseEntity<T>, IAuditableEntity
{
    public DateTimeOffset Created { get; set; } = DateTimeOffset.UtcNow;

    public string? CreatedBy { get; set; }

    public DateTimeOffset LastModified { get; set; } = DateTimeOffset.UtcNow;

    public string? LastModifiedBy { get; set; }
}
