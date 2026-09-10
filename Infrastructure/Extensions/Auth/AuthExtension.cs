using Infrastructure.Notifications.Integration;
using Microsoft.AspNetCore.Authentication;
using System;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace Infrastructure.Extensions.Auth;

public static class AuthExtension
{
    public static IServiceCollection AddInfraAuth(this IServiceCollection services, IConfiguration configuration)
    {
        var jwtSection = configuration.GetSection("Jwt");
        services
            .AddAuthentication("NotificationOrJwt")
            .AddPolicyScheme("NotificationOrJwt", "Notification API key or JWT", options =>
            {
                options.ForwardDefaultSelector = context => context.Request.Headers.Authorization.ToString()
                    .StartsWith("ApiKey ", StringComparison.OrdinalIgnoreCase)
                    ? NotificationApiKeyHandler.SchemeName : JwtBearerDefaults.AuthenticationScheme;
            })
            .AddScheme<AuthenticationSchemeOptions, NotificationApiKeyHandler>(NotificationApiKeyHandler.SchemeName, _ => { })
            .AddJwtBearer(options =>
            {
                var signingKey = jwtSection["SigningKey"]
                    ?? throw new InvalidOperationException("Jwt:SigningKey is not configured.");
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = jwtSection["Issuer"],
                    ValidateAudience = true,
                    ValidAudience = jwtSection["Audience"],
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromMinutes(1)
                };
            });

        services.AddAuthorizationBuilder()
            .AddPolicy("NotificationSend", policy => policy.RequireAuthenticatedUser().RequireAssertion(context =>
                context.User.IsInRole("Admin") || context.User.HasClaim("notification_permission", "send") ||
                !context.User.HasClaim(c => c.Type == "application_id")))
            .AddPolicy("NotificationRead", policy => policy.RequireAuthenticatedUser().RequireAssertion(context =>
                context.User.IsInRole("Admin") || context.User.HasClaim("notification_permission", "read") ||
                !context.User.HasClaim(c => c.Type == "application_id")))
            .AddPolicy(AuthPolicies.RequireAdmin, policy => policy.RequireRole("Admin"));

        services.AddAuthorization(options =>
        {
            options.FallbackPolicy = new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .Build();
        });
        
        return services;
    }
}
