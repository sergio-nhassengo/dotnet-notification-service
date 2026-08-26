using System;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using Serilog.Enrichers.Span;
using Serilog.Events;
using Serilog.Formatting.Json;
using Serilog.Sinks.Grafana.Loki;
using Serilog.Sinks.MSSqlServer;

namespace Infrastructure.Extensions.Logs;

public static class LogsExtension
{
    
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
}
