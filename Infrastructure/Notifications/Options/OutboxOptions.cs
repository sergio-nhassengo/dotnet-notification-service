using System.ComponentModel.DataAnnotations;

namespace Infrastructure.Notifications.Options;

public sealed class OutboxOptions
{
    public const string SectionName = "Outbox";
    [Range(1, 300)] public int PollingIntervalSeconds { get; set; } = 2;
    [Range(1, 365)] public int ProcessedRetentionDays { get; set; } = 7;
}
