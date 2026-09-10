using Confluent.Kafka;
using Infrastructure.Notifications.Options;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Infrastructure.Notifications.Workers;

public sealed class KafkaEmailConsumerWorker(IConsumer<string, string> consumer, KafkaEmailMessageProcessor processor,
    IOptions<KafkaOptions> options, ILogger<KafkaEmailConsumerWorker> logger) : BackgroundService
{
    protected override Task ExecuteAsync(CancellationToken stoppingToken) => Task.Run(() => Consume(stoppingToken), stoppingToken);
    private async Task Consume(CancellationToken ct)
    {
        consumer.Subscribe(options.Value.EmailRequestedTopic);
        try
        {
            while (!ct.IsCancellationRequested)
            {
                ConsumeResult<string, string> item;
                try { item = consumer.Consume(ct); }
                catch (ConsumeException ex)
                {
                    logger.LogError(ex, "Kafka consume failed for {KafkaTopic}", options.Value.EmailRequestedTopic);
                    await Task.Delay(1000, ct);
                    continue;
                }
                await processor.ProcessAsync(item, ct);
                consumer.StoreOffset(item);
                consumer.Commit(item);
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
        finally { consumer.Close(); }
    }
}
