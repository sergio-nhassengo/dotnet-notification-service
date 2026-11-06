namespace Application.Notifications.Models;

public sealed record EmailMessage(Guid MessageId, string RecipientEmail, string? RecipientName,
    string SenderEmail, string? SenderName, string Subject, string HtmlBody, string TextBody)
{
    public IReadOnlyList<Application.Notifications.Models.EmailAddress> To { get; init; } = [];
    public IReadOnlyList<Application.Notifications.Models.EmailAddress> Cc { get; init; } = [];
    public IReadOnlyList<Application.Notifications.Models.DeliveryAttachment> Attachments { get; init; } = [];
}
