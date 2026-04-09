using Juice.Messaging;

namespace Juice.EventBus
{
    /// <summary>
    /// Common fluent interface implemented by all consumer builders (RabbitMQ, local, etc.).
    /// Enables shared extension methods and utilities that work across transports.
    /// </summary>
    public interface IConsumerBuilder
    {
        /// <summary>
        /// Registers a subscription so that events of type <typeparamref name="TEvent"/>
        /// are dispatched to <typeparamref name="THandler"/>.
        /// Also registers <typeparamref name="THandler"/> as a transient DI service.
        /// </summary>
        /// <typeparam name="TEvent">Integration event type to subscribe to.</typeparam>
        /// <typeparam name="THandler">Handler type that processes the event.</typeparam>
        /// <param name="route">
        /// Optional routing hint (topic, queue binding key, or channel key).
        /// Defaults to the event type name when <see langword="null"/>.
        /// </param>
        /// <returns>This builder, for fluent chaining.</returns>
        IConsumerBuilder Subscribe<TEvent, THandler>(string? route = default)
            where TEvent : IIntegrationEvent
            where THandler : class, IIntegrationEventHandler<TEvent>;
    }
}
