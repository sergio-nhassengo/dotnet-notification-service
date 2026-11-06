using Domain.Entities;
using Domain.Enums;

namespace Domain.UnitTests;

public class EmailNotificationTests
{
    private static EmailNotification Create(DateTimeOffset now) => EmailNotification.Create(Guid.NewGuid(), "correlation", "idempotency",
        "sender@example.com", "Sender", "payment-confirmed",
        "{}", null, now);

    [Fact]
    public void Create_starts_pending()
    {
        var value = Create(DateTimeOffset.UtcNow);
        Assert.Equal(NotificationStatus.Pending, value.Status);
        Assert.Equal(0, value.AttemptCount);
        Assert.Equal(value.CreatedAt, value.NextAttemptAt);
    }

    [Fact]
    public void Sent_notification_captures_current_state()
    {
        var value = Create(DateTimeOffset.UtcNow); value.MarkQueued(DateTimeOffset.UtcNow); value.RecordAttempt(DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);
        value.MarkSent( DateTimeOffset.UtcNow);
        Assert.Equal(NotificationStatus.Sent, value.Status); Assert.NotNull(value.LastAttemptAt); Assert.Equal(1, value.AttemptCount); Assert.Null(value.NextAttemptAt);
    }

}
