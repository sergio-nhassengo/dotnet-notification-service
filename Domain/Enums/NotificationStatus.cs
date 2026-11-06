namespace Domain.Enums;

public enum NotificationStatus
{
    Pending,
    Queued,
    RetryScheduled = 3,
    Sent,
    PermanentlyFailed,
    DeadLettered
}

public enum EmailFailureCategory { None, Transient, Permanent, RateLimited, Configuration }
