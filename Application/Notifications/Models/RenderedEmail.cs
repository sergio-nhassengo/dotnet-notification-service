namespace Application.Notifications.Models;

public sealed record RenderedEmail(string Subject, string HtmlBody, string TextBody);
