namespace Juice.EventBus
{
    /// <summary>
    /// Event bus
    /// </summary>
    public interface IEventBus
    {
        /// <summary>
        /// Publish <see cref="IIntegrationEvent"/> to implemented broker like RabbitMQ, ServiceBus...
        /// </summary>
        /// <param name="event"></param>
        /// <param name="tenantId"></param>
        /// <param name="cancellationToken"></param>
        ValueTask PublishAsync<T>(T @event,
            string? tenantId = default, CancellationToken cancellationToken = default)
            where T: IIntegrationEvent;

        /// <summary>
        /// SubscribeAsync an <see cref="IIntegrationEvent"/> with specified <see cref="IIntegrationEventHandler{T}"/>
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <typeparam name="TH"></typeparam>
        ValueTask SubscribeAsync<T, TH>(string? key = default)
            where T : IIntegrationEvent
            where TH : IIntegrationEventHandler<T>;

        /// <summary>
        /// SubscribeAsync an <see cref="IIntegrationEvent"/> with specified <see cref="IIntegrationEventHandler{T}"/>
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <typeparam name="TH"></typeparam>
        ValueTask UnsubscribeAsync<T, TH>(string? key = default)
            where TH : IIntegrationEventHandler<T>
            where T : IIntegrationEvent;

        /// <summary>
        /// Destroy the event bus
        /// </summary>
        /// <returns></returns>
        ValueTask CloseAsync();
    }

    public interface IEventBus<in T>: IEventBus
    {
        
    }
}
