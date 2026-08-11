using Application.Common.Interfaces;
using Application.Common.Security;
using Infra.Extensions.Auth;
using Infra.Extensions.Cors;
using Infra.Extensions.Telemetry;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Infra;

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
