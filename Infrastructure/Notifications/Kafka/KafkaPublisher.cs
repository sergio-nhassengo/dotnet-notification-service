using System.Text;
using Application.Notifications.Interfaces;
using Application.Notifications.Models;
using Confluent.Kafka;

namespace Infrastructure.Notifications.Kafka;

public sealed class KafkaPublisher(IProducer<string, string> producer) : IKafkaPublisher
{
    public async Task PublishAsync(KafkaEnvelope message, CancellationToken ct)
    {
        var kafkaMessage = new Message<string, string> { Key = message.MessageKey.ToString(), Value = message.Payload };
        foreach (var header in message.Headers) kafkaMessage.Headers.Add(header.Key, Encoding.UTF8.GetBytes(header.Value));
        await producer.ProduceAsync(message.Topic, kafkaMessage, ct);
    }
}
