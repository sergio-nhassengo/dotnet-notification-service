using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Infrastructure.Notifications;

public static class NotificationTelemetry
{
    public const string SourceName = "MPDCApiTemplate.Notifications";
    public static readonly ActivitySource ActivitySource = new(SourceName);
    public static readonly Meter Meter = new(SourceName);
    public static readonly Counter<long> Requested = Meter.CreateCounter<long>("notifications_requested_total");
    public static readonly Counter<long> Sent = Meter.CreateCounter<long>("notifications_sent_total");
    public static readonly Counter<long> Failed = Meter.CreateCounter<long>("notifications_failed_total");
    public static readonly Counter<long> Retried = Meter.CreateCounter<long>("notifications_retried_total");
    public static readonly Counter<long> DeadLettered = Meter.CreateCounter<long>("notifications_dead_lettered_total");
    public static readonly Histogram<double> ProviderDuration = Meter.CreateHistogram<double>("email_provider_duration", "ms");
}
