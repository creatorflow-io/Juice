namespace Juice.EventBus
{
    /// <summary>
    /// Event bus
    /// </summary>
    public interface IEventBus
    {
        /// <summary>
        /// Publish <see cref="IntegrationEvent"/> to implemented broker like RabbitMQ, ServiceBus...
        /// </summary>
        /// <param name="event"></param>
        /// <param name="tenantId"></param>
        ValueTask PublishAsync(IntegrationEvent @event, string? tenantId = default);

        /// <summary>
        /// SubscribeAsync an <see cref="IntegrationEvent"/> with specified <see cref="IIntegrationEventHandler{T}"/>
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <typeparam name="TH"></typeparam>
        ValueTask SubscribeAsync<T, TH>(string? key = default)
            where T : IntegrationEvent
            where TH : IIntegrationEventHandler<T>;

        /// <summary>
        /// SubscribeAsync an <see cref="IntegrationEvent"/> with specified <see cref="IIntegrationEventHandler{T}"/>
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <typeparam name="TH"></typeparam>
        ValueTask UnsubscribeAsync<T, TH>(string? key = default)
            where TH : IIntegrationEventHandler<T>
            where T : IntegrationEvent;

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
