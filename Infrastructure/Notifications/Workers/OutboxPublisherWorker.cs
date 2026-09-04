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
        var idleDelay = TimeSpan.FromSeconds(options.Value.PollingIntervalSeconds);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var found = await PublishNext(stoppingToken);
                if (!found) await Task.Delay(idleDelay, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex)
            {
                logger.LogError(ex, "Outbox publisher cycle failed");
                try { await Task.Delay(idleDelay, stoppingToken); }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            }
        }
    }
    private async Task<bool> PublishNext(CancellationToken ct)
    {
        Domain.Entities.OutboxMessage? message;
        using (var scope = scopes.CreateScope()) message = await scope.ServiceProvider.GetRequiredService<ISender>()
            .Send(new GetNextPendingOutboxMessageQuery(clock.Now), ct);
        if (message is null) return false;
        try
        {
            var headers = JsonSerializer.Deserialize<Dictionary<string, string>>(message.Headers) ?? [];
            await publisher.PublishAsync(new KafkaEnvelope(message.MessageKey, message.Topic, message.Payload, headers), ct);
            using var scope = scopes.CreateScope();
            await scope.ServiceProvider.GetRequiredService<ISender>()
                .Send(new CompleteOutboxMessageCommand(message.Id, clock.Now), ct);
            logger.LogInformation("Published outbox message {MessageId} to {KafkaTopic}", message.MessageKey, message.Topic);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            var seconds = Math.Min(300, Math.Pow(2, Math.Min(message.AttemptCount, 8))) * (0.8 + Random.Shared.NextDouble() * 0.4);
            using var scope = scopes.CreateScope();
            await scope.ServiceProvider.GetRequiredService<ISender>()
                .Send(new FailOutboxMessageCommand(message.Id, SafeError.Sanitize(ex.Message), clock.Now.AddSeconds(seconds)), ct);
            logger.LogWarning("Outbox publish failed for {MessageId}; attempt {AttemptNumber}", message.MessageKey, message.AttemptCount + 1);
        }
        return true;
    }
}
