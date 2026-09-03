using Application.Notifications.Contracts;
using Application.Notifications.Models;

namespace Application.Notifications.Interfaces;

public interface IEmailProvider
{
    string Name { get; }
    Task<EmailProviderResult> SendAsync(EmailMessage message, CancellationToken cancellationToken);
}

public interface IEmailTemplateRenderer
{
    Task<RenderedEmail> RenderAsync(string templateId, int version,
        IReadOnlyDictionary<string, string> variables, CancellationToken cancellationToken);
}

public interface IIntegrationEventSerializer
{
    string Serialize<T>(T value);
    bool TryDeserializeEmailRequested(string payload, int schemaVersion, out EmailRequestedV1? value, out string error);
}

public interface IKafkaPublisher
{
    Task PublishAsync(KafkaEnvelope message, CancellationToken cancellationToken);
}

public interface INotificationDefaults
{
    string EmailRequestedTopic { get; }
    string DeadLetterTopic { get; }
    string SenderEmail { get; }
    string? SenderName { get; }
    string? ReplyTo { get; }
    bool IsTemplateAllowed(string templateId);
    bool AllowSubjectOverride { get; }
}
