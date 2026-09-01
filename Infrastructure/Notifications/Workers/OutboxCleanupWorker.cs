using Application.Common.Interfaces;
using Application.Notifications.Interfaces;
using Infrastructure.Notifications.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Infrastructure.Notifications.Workers;

public sealed class OutboxCleanupWorker(IServiceScopeFactory scopes, IOptions<OutboxOptions> options,
    IDateTime clock, ILogger<OutboxCleanupWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromHours(6));
        while (await timer.WaitForNextTickAsync(ct))
        {
            try
            {
                using var scope = scopes.CreateScope(); var count = await scope.ServiceProvider.GetRequiredService<INotificationStore>()
                    .CleanupOutboxAsync(clock.Now.AddDays(-options.Value.ProcessedRetentionDays), ct);
                logger.LogInformation("Removed {OutboxCleanupCount} processed outbox records", count);
            }
            catch (Exception ex) when (!ct.IsCancellationRequested) { logger.LogError(ex, "Outbox cleanup failed"); }
        }
    }
}
