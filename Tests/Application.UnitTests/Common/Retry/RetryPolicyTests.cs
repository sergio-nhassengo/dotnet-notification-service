using Application.Common.Retry;
using Domain.Enums;

namespace Application.UnitTests.Common.Retry;

public class RetryPolicyTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-01-01T00:00:00Z");
    private readonly RetryPolicy _policy = new();

    [Theory]
    [InlineData(1, 5, true)]
    [InlineData(4, 5, true)]
    [InlineData(5, 5, false)]
    [InlineData(6, 5, false)]
    public void Has_attempts_remaining_returns_expected_result(
        int failedAttemptNumber,
        int maximumAttempts,
        bool expected)
    {
        Assert.Equal(expected, _policy.HasAttemptsRemaining(failedAttemptNumber, maximumAttempts));
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(2, 5)]
    [InlineData(3, 15)]
    [InlineData(4, 60)]
    [InlineData(10, 60)]
    public void Get_next_attempt_at_uses_fixed_schedule(int failedAttemptNumber, int expectedMinutes)
    {
        Assert.Equal(Now.AddMinutes(expectedMinutes), _policy.GetNextAttemptAt(Now, failedAttemptNumber));
    }

    [Theory]
    [InlineData(1, 10, 10)]
    [InlineData(2, 2, 5)]
    public void Get_next_attempt_at_treats_provider_delay_as_minimum(
        int failedAttemptNumber,
        int providerMinutes,
        int expectedMinutes)
    {
        Assert.Equal(
            Now.AddMinutes(expectedMinutes),
            _policy.GetNextAttemptAt(Now, failedAttemptNumber, TimeSpan.FromMinutes(providerMinutes)));
    }

    [Fact]
    public void Get_next_attempt_at_uses_default_delay_when_minimum_is_null()
    {
        Assert.Equal(Now.AddMinutes(1), _policy.GetNextAttemptAt(Now, 1, null));
    }

    [Fact]
    public void Get_next_attempt_at_uses_default_delay_when_minimum_is_zero()
    {
        Assert.Equal(Now.AddMinutes(1), _policy.GetNextAttemptAt(Now, 1, TimeSpan.Zero));
    }

    [Fact]
    public void Get_next_attempt_at_rejects_attempt_zero()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => _policy.GetNextAttemptAt(Now, 0));
    }

    [Theory]
    [InlineData(EmailFailureCategory.Transient, true)]
    [InlineData(EmailFailureCategory.RateLimited, true)]
    [InlineData(EmailFailureCategory.Permanent, false)]
    [InlineData(EmailFailureCategory.Configuration, false)]
    [InlineData(EmailFailureCategory.None, false)]
    public void Only_transient_and_rate_limited_failures_are_retryable(EmailFailureCategory category, bool expected)
    {
        Assert.Equal(expected, RetryPolicy.IsRetryable(category));
    }
}
