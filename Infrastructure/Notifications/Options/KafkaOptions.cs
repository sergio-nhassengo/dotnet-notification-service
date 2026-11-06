using System.ComponentModel.DataAnnotations;

namespace Infrastructure.Notifications.Options;

public sealed class KafkaOptions
{
    public const string SectionName = "Kafka";
    public bool Enabled { get; set; } = true;
    [Required] public string BootstrapServers { get; set; } = string.Empty;
    [Required] public string EmailRequestedTopic { get; set; } = "primary-topic-notification";
    [Required] public string DeadLetterTopic { get; set; } = "dlq-topic-notification";
    [Required] public string ConsumerGroup { get; set; } = "notification-service-email-v1";
    public string SecurityProtocol { get; set; } = "Plaintext";
}
