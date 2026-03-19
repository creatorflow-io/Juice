using Juice.Messaging.Context;

namespace Juice.Messaging.Publishing
{
    public interface IMessagePublisher
    {
        string Key { get; }
        /// <summary>
        /// Publish payload to implemented broker like RabbitMQ, Kafka
        /// </summary>
        /// <param name="message"></param>
        /// <param name="context"></param>
        /// <param name="cancellationToken"></param>
        ValueTask PublishAsync(IMessage message, MessageContextData? context, CancellationToken cancellationToken = default);
    }

}
