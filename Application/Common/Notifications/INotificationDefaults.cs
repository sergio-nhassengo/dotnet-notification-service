namespace Application.Common.Notifications;

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
