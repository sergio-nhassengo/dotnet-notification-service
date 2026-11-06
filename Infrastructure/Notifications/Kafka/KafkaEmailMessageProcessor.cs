using Application.Features.Notifications.Commands.AcceptEmailRequested;
using Confluent.Kafka;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace Infrastructure.Notifications.Kafka;

/// <summary>Translates a consumed Kafka record into <see cref="AcceptEmailRequestedCommand"/>.</summary>
public sealed class KafkaEmailMessageProcessor(IServiceScopeFactory scopes)
{
    public async Task ProcessAsync(ConsumeResult<string, string> item, CancellationToken ct)
    {
        using var scope = scopes.CreateScope();
        await scope.ServiceProvider.GetRequiredService<ISender>().Send(new AcceptEmailRequestedCommand(
            item.Topic, item.Partition.Value, item.Offset.Value, item.Message.Key, item.Message.Value), ct);
    }
}
