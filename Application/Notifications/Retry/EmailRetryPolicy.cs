using Domain.Enums;

namespace Application.Notifications.Retry;

public sealed class EmailRetryPolicy
{
    private static readonly TimeSpan[] DefaultDelays =
        [TimeSpan.Zero, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(15), TimeSpan.FromHours(1)];

    public bool ShouldRetry(EmailFailureCategory category, int attemptNumber, int maximumAttempts) =>
        category is EmailFailureCategory.Transient or EmailFailureCategory.RateLimited && attemptNumber < maximumAttempts;

    public DateTimeOffset NextAttempt(DateTimeOffset now, int nextAttemptNumber, TimeSpan? retryAfter = null,
        double jitter = 0)
    {
        var index = Math.Clamp(nextAttemptNumber - 1, 0, DefaultDelays.Length - 1);
        var delay = retryAfter is { } providerDelay && providerDelay > DefaultDelays[index]
            ? providerDelay : DefaultDelays[index];
        var boundedJitter = Math.Clamp(jitter, -0.2, 0.2);
        return now.Add(TimeSpan.FromTicks((long)(delay.Ticks * (1 + boundedJitter))));
    }
}
