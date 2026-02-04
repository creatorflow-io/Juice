namespace Juice.EventBus.Publishing
{
    public interface IEventPublisher
    {
        string Key { get; }
        /// <summary>
        /// Publish <see cref="IIntegrationEvent"/> to implemented broker like RabbitMQ, ServiceBus...
        /// </summary>
        /// <param name="event"></param>
        /// <param name="context"></param>
        /// <param name="cancellationToken"></param>
        ValueTask PublishAsync<T>(T @event, PublishContext context, CancellationToken cancellationToken = default)
            where T : IIntegrationEvent;
    }

}
