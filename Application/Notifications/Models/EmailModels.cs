using Domain.Enums;

namespace Application.Notifications.Models;

public sealed record EmailMessage(Guid MessageId, string RecipientEmail, string? RecipientName,
    string SenderEmail, string? SenderName, string? ReplyTo, string Subject, string HtmlBody, string TextBody);

public sealed record EmailProviderResult(bool IsSuccess, EmailFailureCategory FailureCategory,
    string? ProviderMessageId, int? StatusCode, string? ErrorCode, string? SafeErrorMessage,
    TimeSpan? RetryAfter)
{
    public static EmailProviderResult Success(string? id, int? statusCode = null) =>
        new(true, EmailFailureCategory.None, id, statusCode, null, null, null);
    public static EmailProviderResult Failure(EmailFailureCategory category, string code, string message,
        int? statusCode = null, TimeSpan? retryAfter = null) =>
        new(false, category, null, statusCode, code, message, retryAfter);
}

public sealed record RenderedEmail(string Subject, string HtmlBody, string TextBody);
public sealed record KafkaEnvelope(Guid MessageKey, string Topic, string Payload,
    IReadOnlyDictionary<string, string> Headers);

public sealed record KafkaInboundMessage(string Topic, int Partition, long Offset, Guid MessageKey,
    string Payload, IReadOnlyDictionary<string, string> Headers);
