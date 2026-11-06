using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Encodings.Web;
using System.Text;
using Infrastructure.Notifications.Options;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace MPDCApiTemplate.Authentication;

public sealed class NotificationApiKeyHandler(IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger, UrlEncoder encoder, IOptions<NotificationIntegrationOptions> integration)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "NotificationApiKey";
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var header = Request.Headers.Authorization.ToString();
        if (!header.StartsWith("ApiKey ", StringComparison.OrdinalIgnoreCase))
            return Task.FromResult(AuthenticateResult.NoResult());
        var parts = header[7..].Split('.', 2);
        if (parts.Length != 2 || !integration.Value.Applications.TryGetValue(parts[0], out var app) ||
            !app.Enabled || app.ApiKeySha256.Length != 64)
            return Task.FromResult(AuthenticateResult.Fail("Invalid application credentials."));
        byte[] expected;
        try { expected = Convert.FromHexString(app.ApiKeySha256); }
        catch (FormatException) { return Task.FromResult(AuthenticateResult.Fail("Invalid application credentials.")); }
        var actual = SHA256.HashData(Encoding.UTF8.GetBytes(parts[1]));
        if (!CryptographicOperations.FixedTimeEquals(expected, actual))
            return Task.FromResult(AuthenticateResult.Fail("Invalid application credentials."));
        var identity = new ClaimsIdentity(new[] {
            new Claim(ClaimTypes.NameIdentifier, parts[0]), new Claim("application_id", parts[0])
        }, SchemeName);
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName)));
    }
}
