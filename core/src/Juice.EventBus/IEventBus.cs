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
        Task PublishAsync(IntegrationEvent @event, string? tenantId = default);

        /// <summary>
        /// Subscribe an <see cref="IntegrationEvent"/> with specified <see cref="IIntegrationEventHandler{T}"/>
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <typeparam name="TH"></typeparam>
        void Subscribe<T, TH>(string? key = default)
            where T : IntegrationEvent
            where TH : IIntegrationEventHandler<T>;

        /// <summary>
        /// Subscribe an <see cref="IntegrationEvent"/> with specified <see cref="IIntegrationEventHandler{T}"/>
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <typeparam name="TH"></typeparam>
        void Unsubscribe<T, TH>(string? key = default)
            where TH : IIntegrationEventHandler<T>
            where T : IntegrationEvent;
    }

    public interface IEventBus<in T>: IEventBus
    {
        
    }
}
