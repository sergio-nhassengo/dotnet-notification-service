using Application.Notifications.Models;

namespace Application.Notifications.Contracts;

public sealed record EmailRequestedV1(
    Guid MessageId,
    string CorrelationId,
    string IdempotencyKey,
    string TemplateId,

    IReadOnlyDictionary<string, string> Variables,
    string? Subject,
    DateTimeOffset RequestedAt)
{
    public string? ApplicationId { get; init; }
    public string? RequestHash { get; init; }
    public EmailContent? Content { get; init; }

}

