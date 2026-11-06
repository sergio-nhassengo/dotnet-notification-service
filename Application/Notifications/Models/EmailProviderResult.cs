using Domain.Enums;

namespace Application.Notifications.Models;

public sealed record EmailProviderResult(bool IsSuccess, EmailFailureCategory FailureCategory,
    int? StatusCode, string? ErrorCode, string? SafeErrorMessage,
    TimeSpan? RetryAfter)
{
    public static EmailProviderResult Success(int? statusCode = null) =>
        new(true, EmailFailureCategory.None, statusCode, null, null, null);
    public static EmailProviderResult Failure(EmailFailureCategory category, string code, string message,
        int? statusCode = null, TimeSpan? retryAfter = null) =>
        new(false, category, statusCode, code, message, retryAfter);
}
