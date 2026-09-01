using System.Text.RegularExpressions;

namespace Application.Notifications.Security;

public static partial class SafeError
{
    public static string Sanitize(string? value, int maximumLength = 500)
    {
        if (string.IsNullOrWhiteSpace(value)) return "An external dependency rejected the operation.";
        var safe = SecretPattern().Replace(value, "$1=[REDACTED]");
        safe = EmailPattern().Replace(safe, "***@***");
        return safe.Length <= maximumLength ? safe : safe[..maximumLength];
    }

    [GeneratedRegex("(?i)(api[-_ ]?key|authorization|token|password)\\s*[:=]\\s*[^\\s,;]+")]
    private static partial Regex SecretPattern();
    [GeneratedRegex(@"[A-Z0-9._%+-]+@[A-Z0-9.-]+\.[A-Z]{2,}", RegexOptions.IgnoreCase)]
    private static partial Regex EmailPattern();
}
