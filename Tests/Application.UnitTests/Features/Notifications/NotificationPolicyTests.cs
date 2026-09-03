using Application.Notifications.Retry;
using Application.Notifications.Security;
using Domain.Enums;

namespace Application.UnitTests.Features.Notifications;

public class NotificationPolicyTests
{
    [Theory]
    [InlineData(EmailFailureCategory.Transient, 1, true)]
    [InlineData(EmailFailureCategory.RateLimited, 4, true)]
    [InlineData(EmailFailureCategory.Permanent, 1, false)]
    [InlineData(EmailFailureCategory.Configuration, 1, false)]
    [InlineData(EmailFailureCategory.Transient, 5, false)]
    public void Retry_policy_classifies_failures(EmailFailureCategory category, int attempt, bool expected) =>
        Assert.Equal(expected, new EmailRetryPolicy().ShouldRetry(category, attempt, 5));

    [Fact]
    public void Retry_policy_uses_expected_schedule_and_provider_retry_after()
    {
        var now = DateTimeOffset.Parse("2026-01-01T00:00:00Z"); var policy = new EmailRetryPolicy();
        Assert.Equal(now.AddMinutes(1), policy.NextAttempt(now, 2));
        Assert.Equal(now.AddMinutes(10), policy.NextAttempt(now, 2, TimeSpan.FromMinutes(10)));
    }

    [Fact]
    public void Safe_error_removes_secrets_and_personal_email()
    {
        var safe = SafeError.Sanitize("api-key=secret customer@example.com");
        Assert.DoesNotContain("secret", safe); Assert.DoesNotContain("customer@example.com", safe);
    }
}
