using Application.Common.Interfaces;
using Application.Common.Security;
using Infrastructure.Extensions.Auth;
using Infrastructure.Extensions.Cors;
using Infrastructure.Extensions.Telemetry;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfra(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddInfraOpenTelemetry(configuration);
        services.AddInfraCors(configuration);
        services.AddInfraAuth(configuration);

        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUserService, CurrentUserService>();
        services.AddSingleton<IDateTime, DateTimeService>();

        return services;
    }
}
