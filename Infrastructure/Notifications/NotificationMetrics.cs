using System.Diagnostics.Metrics;
using Application.Common.Interfaces;
using Application.Notifications.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Infrastructure.Notifications;

public sealed class NotificationMetrics(IServiceScopeFactory scopes, IDateTime clock) : BackgroundService
{
    private long _outboxPending, _deliveryPending, _consumerLag = 0;
    private double _outboxAge, _deliveryAge;
    public NotificationMetrics Register()
    {
        NotificationTelemetry.Meter.CreateObservableGauge("outbox_pending_count", () => Interlocked.Read(ref _outboxPending));
        NotificationTelemetry.Meter.CreateObservableGauge("outbox_oldest_pending_age", () => _outboxAge, "s");
        NotificationTelemetry.Meter.CreateObservableGauge("delivery_pending_count", () => Interlocked.Read(ref _deliveryPending));
        NotificationTelemetry.Meter.CreateObservableGauge("delivery_oldest_pending_age", () => _deliveryAge, "s");
        NotificationTelemetry.Meter.CreateObservableGauge("kafka_consumer_lag", () => Interlocked.Read(ref _consumerLag));
        return this;
    }
    public void SetConsumerLag(long lag) => Interlocked.Exchange(ref _consumerLag, Math.Max(0, lag));
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(15));
        do
        {
            try
            {
                using var scope = scopes.CreateScope(); var store = scope.ServiceProvider.GetRequiredService<INotificationStore>();
                var outbox = await store.GetOutboxStatsAsync(clock.Now, ct); var delivery = await store.GetDeliveryStatsAsync(clock.Now, ct);
                Interlocked.Exchange(ref _outboxPending, outbox.Pending); Interlocked.Exchange(ref _deliveryPending, delivery.Pending);
                _outboxAge = outbox.OldestAgeSeconds; _deliveryAge = delivery.OldestAgeSeconds;
            }
            catch when (!ct.IsCancellationRequested) { }
        } while (await timer.WaitForNextTickAsync(ct));
    }
}
