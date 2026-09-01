using Confluent.Kafka;
using Infrastructure.Notifications.Options;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace Infrastructure.Notifications.Health;

public sealed class KafkaHealthCheck(IAdminClient admin) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken ct = default)
    {
        try
        {
            var metadata = admin.GetMetadata(TimeSpan.FromSeconds(3));
            return Task.FromResult(metadata.Brokers.Count > 0 ? HealthCheckResult.Healthy() : HealthCheckResult.Unhealthy("No Kafka brokers available."));
        }
        catch (Exception ex) { return Task.FromResult(HealthCheckResult.Unhealthy("Kafka is unavailable.", ex)); }
    }
}
public sealed class EmailProviderConfigurationHealthCheck(IOptions<EmailProviderOptions> options) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken ct = default)
    {
        var o = options.Value;
        var healthy = o.Provider.Equals("Fake", StringComparison.OrdinalIgnoreCase) ||
                      (!string.IsNullOrWhiteSpace(o.ApiKey) && Uri.TryCreate(o.BaseUrl, UriKind.Absolute, out _));
        return Task.FromResult(healthy ? HealthCheckResult.Healthy() : HealthCheckResult.Unhealthy("Email provider configuration is incomplete."));
    }
}
