namespace Application.Notifications.Models;

public sealed record RenderedEmail(string Subject, string HtmlBody, string TextBody)
{
    public int MinimumAttachments { get; init; }
}
