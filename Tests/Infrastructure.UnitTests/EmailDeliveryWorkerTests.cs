using System.Reflection;
using Application.Common.Retry;
using Domain.Enums;
using Infrastructure.Notifications.Workers;

namespace Infrastructure.UnitTests;

public class EmailDeliveryWorkerTests
{
    [Theory]
    [InlineData(EmailFailureCategory.Transient, 1, true)]
    [InlineData(EmailFailureCategory.RateLimited, 1, true)]
    [InlineData(EmailFailureCategory.Permanent, 1, false)]
    [InlineData(EmailFailureCategory.Configuration, 1, false)]
    [InlineData(EmailFailureCategory.Transient, 5, false)]
    [InlineData(EmailFailureCategory.RateLimited, 5, false)]
    public void Worker_retries_only_eligible_failures_when_attempts_remain(
        EmailFailureCategory failureCategory,
        int failedAttemptNumber,
        bool expected)
    {
        var method = typeof(EmailDeliveryWorker).GetMethod(
            "IsRetryable",
            BindingFlags.NonPublic | BindingFlags.Static);

        Assert.NotNull(method);
        var retryable = Assert.IsType<bool>(method.Invoke(null, [failureCategory]));
        var shouldRetry =
            retryable &&
            new RetryPolicy().HasAttemptsRemaining(failedAttemptNumber, maximumAttempts: 5);

        Assert.Equal(expected, shouldRetry);
    }
}
