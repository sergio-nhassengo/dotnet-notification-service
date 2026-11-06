using Application.Notifications.Models;
using Infrastructure.Notifications.Providers;

namespace Infrastructure.UnitTests;

public sealed class EmailIntegrationAdapterTests
{
    [Fact]
    public void Smtp_message_preserves_recipients_cc_attachments_and_inline_logo()
    {
        using var message = SmtpEmailProvider.CreateMessage(Message() with
        {
            Attachments = [new("ticket.pdf", "application/pdf", [1, 2], null), new("logo.png", "image/png", [3], "logo")]
        });
        Assert.Equal(2, message.To.Count); Assert.Single(message.CC);
        Assert.Single(message.Attachments);
        Assert.Equal("ticket.pdf", message.Attachments[0].Name);
        Assert.Equal("logo", message.AlternateViews[1].LinkedResources[0].ContentId);
    }
    private static EmailMessage Message() => new(Guid.NewGuid(), "a@example.com", null, "sender@example.com", "Sender", "Ticket", "<p>Ticket</p>", "Ticket")
    { To = [new("a@example.com"), new("b@example.com")], Cc = [new("cc@example.com")] };
}
