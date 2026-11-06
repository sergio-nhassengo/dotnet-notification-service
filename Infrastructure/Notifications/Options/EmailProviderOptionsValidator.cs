using Microsoft.Extensions.Options;

namespace Infrastructure.Notifications.Options;

public sealed class EmailProviderOptionsValidator : IValidateOptions<EmailProviderOptions>
{
    public ValidateOptionsResult Validate(string? name, EmailProviderOptions o)
    {
        if (!new[] { "Smtp", "SmtpRelay" }.Contains(o.Provider, StringComparer.OrdinalIgnoreCase))
            return ValidateOptionsResult.Fail("EmailProvider:Provider must be Smtp or SmtpRelay.");
        return ValidateOptionsResult.Success;
    }
}
