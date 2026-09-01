using Domain.Entities;
using Domain.Enums;

namespace Domain.UnitTests;

public class EmailNotificationTests
{
    private static EmailNotification Create(DateTimeOffset now) => EmailNotification.Create(Guid.NewGuid(), "correlation", "idempotency",
        "Customer@Example.com", "Customer", "sender@example.com", "Sender", null, "payment-confirmed", 1,
        "{}", null, NotificationPriority.Normal, NotificationSource.RestApi, now, null, now);

    [Fact]
    public void Create_normalizes_recipient_and_starts_pending()
    {
        var value = Create(DateTimeOffset.UtcNow);
        Assert.Equal("customer@example.com", value.RecipientEmail);
        Assert.Equal(NotificationStatus.Pending, value.Status);
        Assert.Equal(0, value.AttemptCount);
    }

    [Fact]
    public void Sent_notification_captures_provider_message_id()
    {
        var value = Create(DateTimeOffset.UtcNow); value.MarkQueued(DateTimeOffset.UtcNow); value.StartAttempt(DateTimeOffset.UtcNow);
        value.MarkSent("provider-1", DateTimeOffset.UtcNow);
        Assert.Equal(NotificationStatus.Sent, value.Status); Assert.Equal("provider-1", value.ProviderMessageId); Assert.Null(value.NextAttemptAt);
    }

    [Fact]
    public void Only_failed_notifications_can_be_replayed() =>
        Assert.Throws<InvalidOperationException>(() => Create(DateTimeOffset.UtcNow).Replay(DateTimeOffset.UtcNow));
}
