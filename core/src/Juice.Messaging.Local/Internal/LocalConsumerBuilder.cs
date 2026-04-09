using Juice.EventBus;
using Juice.EventBus.Subscriptions;
using Juice.Messaging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Juice.Messaging.Local.Internal
{
    /// <summary>
    /// Builder for registering in-process integration event handlers with the local
    /// subscriptions manager. Each <see cref="Subscribe{TEvent,THandler}"/> call:
    /// <list type="bullet">
    ///   <item>Records the <c>(TEvent → THandler)</c> binding in the subscriptions manager.</item>
    ///   <item>Registers <typeparamref name="THandler"/> as a transient DI service.</item>
    /// </list>
    /// Instances are created by <c>AddLocalConsumer</c> and registered as
    /// <see cref="ILocalSubscriptionsProvider"/> singletons, making them available
    /// to <see cref="Microsoft.Extensions.DependencyInjection.EventBusServiceCollectionExtensions.AddLocalSubscriptionsManager"/>.
    /// </summary>
    public sealed class LocalConsumerBuilder : IConsumerBuilder, ILocalSubscriptionsProvider
    {
        private readonly IServiceCollection _services;
        private readonly List<SubscriptionInfo> _subscriptions = new();

        internal LocalConsumerBuilder(IServiceCollection services)
        {
            _services = services;
        }

        /// <summary>
        /// Registers <typeparamref name="THandler"/> to handle events of type
        /// <typeparamref name="TEvent"/> on local and local-channel routes.
        /// </summary>
        /// <param name="key">
        /// Optional routing key override. Defaults to <c>typeof(TEvent).Name</c>.
        /// </param>
        public LocalConsumerBuilder Subscribe<TEvent, THandler>(string? key = null)
            where TEvent : IIntegrationEvent
            where THandler : class, IIntegrationEventHandler<TEvent>
        {
            _subscriptions.Add(SubscriptionInfo.Typed(typeof(TEvent), typeof(THandler), key));
            _services.TryAddTransient<THandler>();
            return this;
        }

        IConsumerBuilder IConsumerBuilder.Subscribe<TEvent, THandler>(string? route)
            => Subscribe<TEvent, THandler>(route);

        /// <inheritdoc />
        IEnumerable<SubscriptionInfo> ISubscriptionsProvider.GetSubscriptions() => _subscriptions;
    }
}
