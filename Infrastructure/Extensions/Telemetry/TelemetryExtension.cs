using System;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Exporter;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Infrastructure.Extensions.Telemetry;

public static class TelemetryExtension
{
    private const string DEFAULT_ENDPOINT = "https://localhost:4317";
    private const string DEFAULT_APP_NAME = "MPDCApiTemplate";

    public static IServiceCollection AddInfraOpenTelemetry(this IServiceCollection services,
        IConfiguration configuration)
    {
        var serviceName = configuration["OpenTelemetry:ServiceName"] ?? DEFAULT_APP_NAME;
        var otlpEndpoint = configuration["OpenTelemetry:Otlp:Endpoint"] ?? DEFAULT_ENDPOINT;

        services.AddOpenTelemetry()
            .ConfigureResource(rb => rb.AddService(serviceName))
            .WithTracing(builder =>
            {
                builder.SetResourceBuilder(ResourceBuilder.CreateDefault().AddService(serviceName))
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddSqlClientInstrumentation();

                if (!string.IsNullOrEmpty(otlpEndpoint))
                {
                    builder.AddOtlpExporter(opt =>
                    {
                        opt.Endpoint = new Uri(otlpEndpoint);
                        opt.Protocol = OtlpExportProtocol.Grpc;
                    });
                }
                else
                {
                    builder.AddConsoleExporter();
                }
            })
            .WithMetrics(builder =>
            {
                builder.AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddSqlClientInstrumentation()
                    .AddHangfireInstrumentation();

                if (!string.IsNullOrEmpty(otlpEndpoint))
                {
                    builder.AddOtlpExporter(opt =>
                    {
                        opt.Endpoint = new Uri(otlpEndpoint);
                        opt.Protocol = OtlpExportProtocol.Grpc;
                    });
                }
                else
                {
                    builder.AddConsoleExporter();
                }
            })
            .WithLogging(builder =>
                {
                    if (!string.IsNullOrEmpty(otlpEndpoint))
                    {
                        builder.AddOtlpExporter(opt =>
                        {
                            opt.Endpoint = new Uri(otlpEndpoint);
                            opt.Protocol = OtlpExportProtocol.Grpc;
                        });
                    }
                    else
                    {
                        builder.AddConsoleExporter();
                    }
                }
            );

        return services;
    }
}
