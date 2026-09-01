using Domain.Common;

namespace Domain.Entities;

public sealed class InboxMessage : BaseEntity<Guid>
{
    public Guid MessageId { get; set; }
    public string ConsumerName { get; set; } = string.Empty;
    public string Topic { get; set; } = string.Empty;
    public int Partition { get; set; }
    public long Offset { get; set; }
    public DateTimeOffset ReceivedAt { get; set; }
    public DateTimeOffset? ProcessedAt { get; set; }
    public string PayloadHash { get; set; } = string.Empty;
}
