using Application.Notifications.Models;

namespace Application.Notifications.Abstractions;

public interface IEmailProvider
{
    string Name { get; }

    Task<EmailProviderResult> SendAsync(
        EmailMessage message,
        CancellationToken cancellationToken);
}
