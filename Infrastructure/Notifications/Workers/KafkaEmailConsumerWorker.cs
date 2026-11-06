using Confluent.Kafka;
using Infrastructure.Notifications.Kafka;
using Infrastructure.Notifications.Options;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Infrastructure.Notifications.Workers;

public sealed class KafkaEmailConsumerWorker(IConsumer<string, string> consumer, KafkaEmailMessageProcessor processor,
    IOptions<KafkaOptions> options, ILogger<KafkaEmailConsumerWorker> logger) : BackgroundService
{
    public async Task ProcessAndCommitAsync(ConsumeResult<string, string> item, CancellationToken ct)
    {
        await processor.ProcessAsync(item, ct);
        consumer.Commit(item);
    }

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
                // Retry this same record until persistence and commit succeed. Consuming a
                // later record could otherwise commit past this failed offset.
                while (!ct.IsCancellationRequested)
                {
                    try
                    {
                        await ProcessAndCommitAsync(item, ct);
                        break;
                    }
                    catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Kafka record persistence or commit failed for {Topic} {Partition} {Offset}",
                            item.Topic, item.Partition.Value, item.Offset.Value);
                        await Task.Delay(1000, ct);
                    }
                }
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
        finally { consumer.Close(); }
    }
}
