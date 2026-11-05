namespace Juice.EventBus.Publishing
{
    public interface ITransportPublisher
    {
        string Key { get; }

        /// <summary>
        /// Whether outbox delivery may call <see cref="PublishAsync"/> for several messages at once on this instance.
        /// False by default: delivery then sends one message at a time.
        /// </summary>
        bool SupportsConcurrentPublish => false;

        /// <summary>
        /// Publish payload to implemented broker like RabbitMQ, Kafka
        /// </summary>
        /// <param name="payload"></param>
        /// <param name="context"></param>
        /// <param name="cancellationToken"></param>
        ValueTask PublishAsync(byte[] payload, PublishContext context, CancellationToken cancellationToken = default);
    }

}
