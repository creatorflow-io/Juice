namespace Juice.EventBus.Publishing
{
    public interface ITransportPublisher
    {
        string Key { get; }
        /// <summary>
        /// Publish payload to implemented broker like RabbitMQ, Kafka
        /// </summary>
        /// <param name="payload"></param>
        /// <param name="context"></param>
        /// <param name="cancellationToken"></param>
        ValueTask PublishAsync(byte[] payload, PublishContext context, CancellationToken cancellationToken = default);
    }

}
