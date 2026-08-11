using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using Serilog.Enrichers.Span;
using Serilog.Events;
using Serilog.Formatting.Json;
using Serilog.Sinks.Grafana.Loki;
using Serilog.Sinks.MSSqlServer;

namespace Infra.Extensions.Logs;

public static class LogsExtension
{
    /// <summary>
    /// Configures the Serilog pipeline for the host. Called from <c>builder.Host.UseSerilog(...)</c>
    /// in Program.cs, since Serilog wraps the whole generic host rather than just <see cref="IServiceCollection"/>.
    /// </summary>
    public static LoggerConfiguration ConfigureInfraSerilog(this LoggerConfiguration loggerConfiguration, IConfiguration configuration)
    {
        loggerConfiguration
            .ReadFrom.Configuration(configuration)
            .Enrich.FromLogContext()
            .Enrich.WithSpan()
            .WriteTo.Console()
            .WriteTo.File("logs/log-.txt", rollingInterval: RollingInterval.Day);
        
        Serilog.Debugging.SelfLog.Enable(msg => Console.Error.WriteLine(msg));
        var loggingConnectionString = configuration.GetConnectionString("Logging");

        if (!string.IsNullOrWhiteSpace(loggingConnectionString))
        {
            loggerConfiguration.WriteTo.MSSqlServer(
                connectionString: loggingConnectionString,
                sinkOptions: new MSSqlServerSinkOptions
                {
                    TableName = "Logs",
                    AutoCreateSqlTable = true
                });
        }

        return loggerConfiguration;
    }

    public static IServiceCollection AddInfraSerilog(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSerilog(options =>
        {
            options.MinimumLevel.Information()
                .WriteTo.Console(new JsonFormatter(), LogEventLevel.Debug);
        });
        
        return services;
    }
}
