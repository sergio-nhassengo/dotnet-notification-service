namespace Domain.Enums;

public enum NotificationStatus
{
    Pending,
    Queued,
    Processing,
    RetryScheduled,
    Sent,
    PermanentlyFailed,
    DeadLettered
}

public enum NotificationPriority { Low, Normal, High }
public enum NotificationSource { RestApi, Kafka }
public enum DeliveryOutcome { Succeeded, TransientFailure, PermanentFailure, RateLimited, ConfigurationFailure }
public enum EmailFailureCategory { None, Transient, Permanent, RateLimited, Configuration }
