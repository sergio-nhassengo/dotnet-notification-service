using Application.Notifications.Interfaces;
using Application.Notifications.Models;

namespace Infrastructure.Notifications.Providers;

public sealed class FakeEmailProvider : IEmailProvider
{
    public string Name => "Fake";
    public Task<EmailProviderResult> SendAsync(EmailMessage message, CancellationToken cancellationToken) =>
        Task.FromResult(EmailProviderResult.Success($"fake-{message.MessageId:N}", 202));
}
