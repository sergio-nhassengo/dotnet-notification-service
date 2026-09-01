using System.ComponentModel.DataAnnotations;

namespace Infrastructure.Notifications.Options;

public sealed class KafkaOptions
{
    public const string SectionName = "Kafka";
    [Required] public string BootstrapServers { get; set; } = string.Empty;
    [Required] public string EmailRequestedTopic { get; set; } = "primary-topic-notification";
    [Required] public string DeadLetterTopic { get; set; } = "dlq-topic-notification";
    [Required] public string ConsumerGroup { get; set; } = "notification-service-email-v1";
    public string SecurityProtocol { get; set; } = "Plaintext";
    public bool EnableIdempotence { get; set; } = true;
    public string Acks { get; set; } = "All";
}
public sealed class OutboxOptions
{
    public const string SectionName = "Outbox";
    [Range(1, 1000)] public int BatchSize { get; set; } = 100;
    [Range(1, 300)] public int PollingIntervalSeconds { get; set; } = 2;
    [Range(1, 365)] public int ProcessedRetentionDays { get; set; } = 7;
}
public sealed class EmailDeliveryOptions
{
    public const string SectionName = "EmailDelivery";
    [Range(1, 1000)] public int BatchSize { get; set; } = 50;
    [Range(1, 100)] public int MaximumConcurrency { get; set; } = 10;
    [Range(1, 20)] public int MaximumAttempts { get; set; } = 5;
    [Range(1, 300)] public int PollingIntervalSeconds { get; set; } = 2;
}
public sealed class EmailProviderOptions
{
    public const string SectionName = "EmailProvider";
    [Required] public string Provider { get; set; } = "Fake";
    public string BaseUrl { get; set; } = "https://api.brevo.com/v3/";
    public string ApiKey { get; set; } = string.Empty;
    [EmailAddress, Required] public string DefaultSenderEmail { get; set; } = "no-reply@example.invalid";
    public string DefaultSenderName { get; set; } = "Notification Service";
    public string? ReplyTo { get; set; }
    public string[] AllowedSenderEmails { get; set; } = [];
    public string[] AllowedTemplateIds { get; set; } = ["payment-confirmed"];
    public bool AllowSubjectOverride { get; set; }
    [Range(1, 120)] public int TimeoutSeconds { get; set; } = 15;
}
public sealed class TemplateOptions
{
    public const string SectionName = "EmailTemplates";
    [Required] public string RootPath { get; set; } = "Templates";
}
