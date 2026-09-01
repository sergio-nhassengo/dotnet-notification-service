using Microsoft.Extensions.Options;

namespace Infrastructure.Notifications.Options;

public sealed class EmailProviderOptionsValidator : IValidateOptions<EmailProviderOptions>
{
    public ValidateOptionsResult Validate(string? name, EmailProviderOptions o)
    {
        if (!new[] { "Fake", "Brevo" }.Contains(o.Provider, StringComparer.OrdinalIgnoreCase))
            return ValidateOptionsResult.Fail("EmailProvider:Provider must be Fake or Brevo.");
        if (o.Provider.Equals("Brevo", StringComparison.OrdinalIgnoreCase) && (string.IsNullOrWhiteSpace(o.ApiKey) || string.IsNullOrWhiteSpace(o.BaseUrl)))
            return ValidateOptionsResult.Fail("Brevo requires EmailProvider:ApiKey and EmailProvider:BaseUrl.");
        if (o.AllowedSenderEmails.Length > 0 && !o.AllowedSenderEmails.Contains(o.DefaultSenderEmail, StringComparer.OrdinalIgnoreCase))
            return ValidateOptionsResult.Fail("EmailProvider:DefaultSenderEmail must be allow-listed.");
        return ValidateOptionsResult.Success;
    }
}
