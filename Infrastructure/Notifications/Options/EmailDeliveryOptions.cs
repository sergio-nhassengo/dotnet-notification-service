using System.ComponentModel.DataAnnotations;

namespace Infrastructure.Notifications.Options;

public sealed class EmailDeliveryOptions
{
    public const string SectionName = "EmailDelivery";
    [Range(1, 20)] public int MaximumAttempts { get; set; } = 5;
    [Range(1, 300)] public int PollingIntervalSeconds { get; set; } = 2;
}
