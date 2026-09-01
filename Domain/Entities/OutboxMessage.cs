using Domain.Common;

namespace Domain.Entities;

public sealed class OutboxMessage : BaseEntity<Guid>
{
    public string EventType { get; set; } = string.Empty;
    public int SchemaVersion { get; set; }
    public Guid MessageKey { get; set; }
    public string Topic { get; set; } = string.Empty;
    public string Payload { get; set; } = string.Empty;
    public string Headers { get; set; } = "{}";
    public DateTimeOffset OccurredAt { get; set; }
    public DateTimeOffset? ProcessedAt { get; set; }
    public int AttemptCount { get; set; }
    public DateTimeOffset NextAttemptAt { get; set; }
    public string? LastError { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public string? LeaseOwner { get; set; }
    public DateTimeOffset? LeaseExpiresAt { get; set; }
    public byte[] RowVersion { get; set; } = [];
}
