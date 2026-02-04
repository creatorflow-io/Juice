using Juice.EventBus;
using Juice.EventBus.Delivery;
using Juice.EventBus.Dispatching;
using Juice.EventBus.Internal;
using Juice.EventBus.Publishing.Policies;
using Juice.EventBus.Publishing.Policies.Internal;
using Juice.EventBus.Subscriptions;
using Juice.EventBus.Transactional;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;


namespace Microsoft.Extensions.DependencyInjection
{
    public static class EventBusServiceCollectionExtensions
    {
        public static EventBusBuilder AddEventBus(this IServiceCollection services)
        {
            return new EventBusBuilder(services).AddDefaultServices();
        }
    }

    public class EventBusBuilder
    {
        public IServiceCollection Services => _services;
        private readonly IServiceCollection _services;
        internal EventBusBuilder(IServiceCollection services)
        {
            _services = services;
        }

        public EventBusBuilder AddDefaultServices()
        {
            _services.TryAddSingleton<IEventSerializer, NewtonsoftSerializer>();
            return this;
        }

        #region Consumer services
        /// <summary>
        /// Registers the default consumer-related services required for event bus integration.
        /// </summary>
        /// <remarks>This method adds the necessary services for event consumption, including the
        /// integration event dispatcher and the in-memory event bus subscriptions manager, to the dependency injection
        /// container. It is intended to be called during event bus policies to enable event handling
        /// capabilities.</remarks>
        /// <returns>The current <see cref="EventBusBuilder"/> instance, enabling method chaining.</returns>
        public EventBusBuilder AddConsumerServices()
        {
            Services.TryAddTransient<IntegrationEventDispatcher>();
            Services.TryAddSingleton<IEventBusSubscriptionsManager>(sp =>
            {
                var logger = sp.GetRequiredService<ILogger<InMemoryEventBusSubscriptionsManager>>();
                return new InMemoryEventBusSubscriptionsManager(logger, true);
            });

            return this;
        }
        #endregion

        #region Outbox delivery

        /// <summary>
        /// Add outbox support for transactional event publishing.
        /// </summary>
        /// <param name="configure"></param>
        /// <returns></returns>
        public EventBusBuilder AddOutboxCore(Action<OutboxBuilder>? configure = default)
        {
            var outboxBuilder = new OutboxBuilder(_services);
            configure?.Invoke(outboxBuilder);

            outboxBuilder.AddDefaultServices();

            return this;
        }

        /// <summary>
        /// Add delivery processing.
        /// </summary>
        /// <param name="configure"></param>
        /// <returns></returns>
        public EventBusBuilder AddDeliveryCore(Action<DeliveryBuilder>? configure = default)
        {
            var deliveryBuilder = new DeliveryBuilder(_services);
            configure?.Invoke(deliveryBuilder);

            deliveryBuilder.BuildEventTypeRegistry();
            return this;
        }

        #endregion

        #region Producer services

        /// <summary>
        /// Add core services required for event publishing.
        /// </summary>
        /// <param name="policies">Configuration section for publishing policies.</param>
        /// <returns></returns>
        public EventBusBuilder AddProducerServices(IConfigurationSection policies)
        {
            _services.TryAddSingleton<IEventBus, CompositeEventPublisher>();

            if (_services.Any(sd => sd.ServiceType == typeof(IEventPublishingPolicy)))
            {
                return this;
            }
            _services.Configure<PublishingPolicyOptions>(policies);
            _services.AddSingleton<IEventPublishingPolicy, DefaultEventPublishingPolicy>();
            return this;
        }

        #endregion
    }
}
