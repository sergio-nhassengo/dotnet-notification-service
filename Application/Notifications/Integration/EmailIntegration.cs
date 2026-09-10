using Application.Notifications.Models;
using Domain.Common;

namespace Application.Notifications.Integration;

public sealed record EmailAddress(string Email, string? Name = null);
public sealed record EmailAttachmentReference(Guid AttachmentId, string? ContentId = null);
public sealed record EmailContent
{
    public IReadOnlyList<EmailAddress> To { get; init; } = [];
    public IReadOnlyList<EmailAddress> Cc { get; init; } = [];
    public IReadOnlyList<EmailAttachmentReference> Attachments { get; init; } = [];
    public string? HtmlBody { get; init; }
    public string? TextBody { get; init; }
}
public sealed record DeliveryAttachment(string FileName, string ContentType, byte[] Content, string? ContentId);
public sealed record NotificationApplication(string Id, string SenderEmail, string? SenderName, string? ReplyTo,
    IReadOnlyList<string> TemplateIds, bool AllowRawContent, bool AllowSubjectOverride, int MaximumRecipients,
    int MaximumAttachments, long MaximumAttachmentBytes);
public interface INotificationApplications
{
    NotificationApplication? Find(string applicationId);
    bool UseKafka { get; }
}
public interface INotificationCaller
{
    string ApplicationId { get; }
    bool IsAdmin { get; }
}
public interface IEmailTemplateRenderer
{
    Task<RenderedEmail> RenderAsync(string templateId, IReadOnlyDictionary<string, string> variables, CancellationToken ct);
}
public interface IEmailPreflight
{
    Task<Error?> ValidateAsync(NotificationApplication application, string? templateId,
        IReadOnlyDictionary<string, string> variables, string? subject, EmailContent content, CancellationToken ct);
}
