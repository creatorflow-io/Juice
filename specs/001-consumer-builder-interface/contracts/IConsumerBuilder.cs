// Assembly: Juice.EventBus
// Namespace: Juice.EventBus
// File: core/src/Juice.EventBus/IConsumerBuilder.cs
//
// This is the intended public contract — not compilable as a standalone file.

using Juice.Messaging.Contracts;

namespace Juice.EventBus;

/// <summary>
/// Common fluent interface for all consumer builders.
/// Enables shared extension methods and utilities across different transports.
/// </summary>
public interface IConsumerBuilder
{
    /// <summary>
    /// Registers a subscription: when an event of type <typeparamref name="TEvent"/>
    /// arrives, dispatch it to <typeparamref name="THandler"/>.
    /// Also registers <typeparamref name="THandler"/> as a transient DI service.
    /// </summary>
    /// <typeparam name="TEvent">Integration event type to subscribe to.</typeparam>
    /// <typeparam name="THandler">Handler type that processes the event.</typeparam>
    /// <param name="route">
    /// Optional routing hint (topic, queue binding key, or channel key).
    /// Defaults to the event type name when null.
    /// </param>
    /// <returns>This builder, for fluent chaining.</returns>
    IConsumerBuilder Subscribe<TEvent, THandler>(string? route = default)
        where TEvent : IIntegrationEvent
        where THandler : class, IIntegrationEventHandler<TEvent>;
}
