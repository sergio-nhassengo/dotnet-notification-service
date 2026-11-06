using Application.Common.Interfaces;
using Application.Features.Notifications.Commands.DeliverEmail;
using Application.Features.Notifications.Queries.GetNextDueEmailDelivery;
using Infrastructure.Notifications.Options;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Infrastructure.Notifications.Workers;

/// <summary>Delivers due emails one at a time, oldest first; delivery rules live in <see cref="DeliverEmailCommand"/>.</summary>
public sealed class EmailDeliveryWorker(IServiceScopeFactory scopes, IOptions<EmailDeliveryOptions> deliveryOptions,
    IDateTime clock, ILogger<EmailDeliveryWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var idleDelay = TimeSpan.FromSeconds(deliveryOptions.Value.PollingIntervalSeconds);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var found = await ProcessNext(stoppingToken);
                if (!found) await Task.Delay(idleDelay, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex)
            {
                logger.LogError(ex, "Email delivery worker cycle failed");
                try { await Task.Delay(idleDelay, stoppingToken); }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            }
        }
    }

    private async Task<bool> ProcessNext(CancellationToken ct)
    {
        Guid notificationId;
        using (var scope = scopes.CreateScope())
        {
            var due = await scope.ServiceProvider.GetRequiredService<ISender>()
                .Send(new GetNextDueEmailDeliveryQuery(clock.Now), ct);
            if (due is null) return false;
            notificationId = due.Id;
        }
        using (var scope = scopes.CreateScope())
            await scope.ServiceProvider.GetRequiredService<ISender>().Send(new DeliverEmailCommand(notificationId), ct);
        return true;
    }
}
