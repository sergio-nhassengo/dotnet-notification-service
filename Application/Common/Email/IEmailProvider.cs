using Application.Notifications.Models;

namespace Application.Common.Email;

public interface IEmailProvider
{
    string Name { get; }

    Task<EmailProviderResult> SendAsync(
        EmailMessage message,
        CancellationToken cancellationToken);
}
