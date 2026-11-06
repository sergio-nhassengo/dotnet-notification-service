using Domain.Common;

namespace Application.Notifications.Models;

public sealed record EmailContent
{
    public IReadOnlyList<EmailAddress> To { get; init; } = [];
    public IReadOnlyList<EmailAddress> Cc { get; init; } = [];
    public string? HtmlBody { get; init; }
    public string? TextBody { get; init; }
}
