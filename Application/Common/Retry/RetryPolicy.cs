namespace Application.Common.Retry;

public sealed class RetryPolicy
{
    private static readonly TimeSpan[] RetryDelays =
    [
        TimeSpan.FromMinutes(1),
        TimeSpan.FromMinutes(5),
        TimeSpan.FromMinutes(15),
        TimeSpan.FromHours(1)
    ];

    public bool HasAttemptsRemaining(
        int failedAttemptNumber,
        int maximumAttempts)
    {
        return failedAttemptNumber < maximumAttempts;
    }

    public DateTimeOffset GetNextAttemptAt(
        DateTimeOffset now,
        int failedAttemptNumber,
        TimeSpan? minimumDelay = null)
    {
        if (failedAttemptNumber < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(failedAttemptNumber),
                "The failed attempt number must be at least one.");
        }

        var index = Math.Clamp(
            failedAttemptNumber - 1,
            0,
            RetryDelays.Length - 1);

        var delay = RetryDelays[index];

        if (minimumDelay is { } value && value > delay)
        {
            delay = value;
        }

        return now.Add(delay);
    }
}
