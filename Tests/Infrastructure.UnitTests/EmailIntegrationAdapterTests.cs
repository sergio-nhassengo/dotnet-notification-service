using Application.Notifications.Models;
using Infrastructure.Notifications.Providers;

namespace Infrastructure.UnitTests;

public sealed class EmailIntegrationAdapterTests
{
    [Fact]
    public void Smtp_message_preserves_recipients_cc_and_both_bodies()
    {
        using var message = SmtpEmailProvider.CreateMessage(Message());
        Assert.Equal(2, message.To.Count); Assert.Single(message.CC);
        Assert.Empty(message.Attachments);
        Assert.Equal(2, message.AlternateViews.Count);
    }
    private static EmailMessage Message() => new(Guid.NewGuid(), "a@example.com", null, "sender@example.com", "Sender", "Ticket", "<p>Ticket</p>", "Ticket")
    { To = [new("a@example.com"), new("b@example.com")], Cc = [new("cc@example.com")] };
}
