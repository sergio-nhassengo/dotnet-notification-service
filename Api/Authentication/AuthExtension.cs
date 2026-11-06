using Application.Notifications.Abstractions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;

namespace MPDCApiTemplate.Authentication;

public static class AuthExtension
{
    /// <summary>
    /// Authenticates every endpoint with an application API key and exposes the authenticated
    /// application to the rest of the service through <see cref="INotificationCaller"/>.
    /// </summary>
    public static IServiceCollection AddApiKeyAuthentication(this IServiceCollection services)
    {
        services.AddAuthentication(NotificationApiKeyHandler.SchemeName)
            .AddScheme<AuthenticationSchemeOptions, NotificationApiKeyHandler>(NotificationApiKeyHandler.SchemeName, _ => { });
        services.AddAuthorization(options => options.FallbackPolicy = new AuthorizationPolicyBuilder()
            .RequireAuthenticatedUser().Build());
        services.AddHttpContextAccessor();
        services.AddScoped<INotificationCaller, NotificationCaller>();
        return services;
    }
}
