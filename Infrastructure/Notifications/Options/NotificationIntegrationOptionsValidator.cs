using Application.Features.Notifications.Commands.CreateEmail;
using Microsoft.Extensions.Options;

namespace Infrastructure.Notifications.Options;

public sealed class NotificationIntegrationOptionsValidator : IValidateOptions<NotificationIntegrationOptions>
{
    public ValidateOptionsResult Validate(string? name, NotificationIntegrationOptions options)
    {
        foreach (var (id, app) in options.Applications)
        {
            if (!System.Text.RegularExpressions.Regex.IsMatch(id, "^[a-z0-9][a-z0-9-]{0,99}$"))
                return ValidateOptionsResult.Fail("Application IDs must be lowercase safe identifiers.");
            if (!app.Enabled) continue;
            if (!CreateEmailNotificationCommandValidator.ValidEmail(app.SenderEmail) ||
                app.SenderName?.IndexOfAny(['\r', '\n']) >= 0 || app.SenderName?.Length > 200)
                return ValidateOptionsResult.Fail($"Application {id} has invalid sender settings.");
            if (app.MaximumRecipients is < 1 or > 100)
                return ValidateOptionsResult.Fail($"Application {id} has an invalid recipient limit.");
            if (app.ApiKeySha256.Length != 0 && !System.Text.RegularExpressions.Regex.IsMatch(app.ApiKeySha256, "^[a-fA-F0-9]{64}$"))
                return ValidateOptionsResult.Fail($"Application {id} API key hash must be a SHA-256 hex digest.");
        }
        return ValidateOptionsResult.Success;
    }
}
