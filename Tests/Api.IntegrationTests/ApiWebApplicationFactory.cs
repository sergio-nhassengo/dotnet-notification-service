using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Infrastructure.Notifications.Workers;
using Persistence;
using Testcontainers.MsSql;

namespace Api.IntegrationTests;

public class ApiWebApplicationFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly MsSqlContainer _dbContainer = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, config) =>
        {
            // The container's mapped port is only known once it has started, so this factory
            // must be started (see InitializeAsync) before the host is built.
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Default"] = _dbContainer.GetConnectionString(),
                ["ConnectionStrings:Logging"] = string.Empty
                ,
                ["OpenTelemetry:Otlp:Endpoint"] = string.Empty
            });
        });
        builder.ConfigureServices(services =>
        {
            var workerTypes = new HashSet<Type>
            {
                typeof(OutboxPublisherWorker), typeof(KafkaEmailConsumerWorker),
                typeof(EmailDeliveryWorker), typeof(OutboxCleanupWorker)
            };
            foreach (var descriptor in services.Where(x => x.ServiceType == typeof(IHostedService) &&
                         x.ImplementationType is not null && workerTypes.Contains(x.ImplementationType)).ToArray())
                services.Remove(descriptor);
        });
    }

    public async Task InitializeAsync()
    {
        await _dbContainer.StartAsync();

        using var scope = Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await context.Database.MigrateAsync();
    }

    public new async Task DisposeAsync()
    {
        await _dbContainer.DisposeAsync();
        await base.DisposeAsync();
    }
}
