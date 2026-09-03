using System.Text.Json;
using Application.Common.Interfaces;
using Application.Features.Notifications.Commands.Outbox;
using Application.Features.Notifications.Queries.Outbox;
using Application.Notifications.Interfaces;
using Application.Notifications.Models;
using Application.Notifications.Security;
using Infrastructure.Notifications.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MediatR;

namespace Infrastructure.Notifications.Workers;

public sealed class OutboxPublisherWorker(IServiceScopeFactory scopes, IKafkaPublisher publisher,
    IOptions<OutboxOptions> options, IDateTime clock, ILogger<OutboxPublisherWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(options.Value.PollingIntervalSeconds));
        do
        {
            try { await PublishBatch(stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex) { logger.LogError(ex, "Outbox publisher cycle failed"); }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }
    private async Task PublishBatch(CancellationToken ct)
    {
        IReadOnlyList<Domain.Entities.OutboxMessage> rows;
        using (var scope = scopes.CreateScope()) rows = await scope.ServiceProvider.GetRequiredService<ISender>()
            .Send(new GetPendingOutboxBatchQuery(options.Value.BatchSize, clock.Now), ct);
        foreach (var row in rows)
        {
            try
            {
                var headers = JsonSerializer.Deserialize<Dictionary<string, string>>(row.Headers) ?? [];
                await publisher.PublishAsync(new KafkaEnvelope(row.MessageKey, row.Topic, row.Payload, headers), ct);
                using var scope = scopes.CreateScope();
                await scope.ServiceProvider.GetRequiredService<ISender>()
                    .Send(new CompleteOutboxMessageCommand(row.Id, clock.Now), ct);
                logger.LogInformation("Published outbox message {MessageId} to {KafkaTopic}", row.MessageKey, row.Topic);
            }
            catch (Exception ex) when (!ct.IsCancellationRequested)
            {
                var seconds = Math.Min(300, Math.Pow(2, Math.Min(row.AttemptCount, 8))) * (0.8 + Random.Shared.NextDouble() * 0.4);
                using var scope = scopes.CreateScope();
                await scope.ServiceProvider.GetRequiredService<ISender>()
                    .Send(new FailOutboxMessageCommand(row.Id, SafeError.Sanitize(ex.Message), clock.Now.AddSeconds(seconds)), ct);
                logger.LogWarning("Outbox publish failed for {MessageId}; attempt {AttemptNumber}", row.MessageKey, row.AttemptCount + 1);
            }
        }
    }
}
