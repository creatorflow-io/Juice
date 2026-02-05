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
        public static EventBusBuilder AddEventBus(this IServiceCollection services, Action<EventBusBuilder>? configure = default)
        {
            var builder = new EventBusBuilder(services);
            configure?.Invoke(builder);
            builder.AddDefaultServices();
            return builder;
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
        /// Add core services required for event consuming.
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public EventBusBuilder AddConsumerServices(string key)
        {
            Services.TryAddTransient<IntegrationEventDispatcher>();
            Services.TryAddKeyedSingleton<ISubscriptionsManager>(key, (sp, k) =>
            {
                var logger = sp.GetRequiredService<ILoggerFactory>()
                    .CreateLogger(typeof(InMemorySubscriptionsManager).Name + $"[{k}]");
                var providers = sp.GetServices<ISubscriptionsProvider>();
                return new InMemorySubscriptionsManager(providers, logger, true);
            });

            return this;
        }

        /// <summary>
        /// Add core services required for event consuming and register global event subscriptions.
        /// </summary>
        /// <param name="subscription"></param>
        /// <returns></returns>
        public EventBusBuilder AddConsumerServices(Action<SubscriptionBuilder>? subscription)
        {
            Services.TryAddTransient<IntegrationEventDispatcher>();
            
            var subscriptionBuilder = new SubscriptionBuilder();
            subscription?.Invoke(subscriptionBuilder);
            if (subscriptionBuilder.HasSubscriptions)
            {
                foreach (var descriptor in subscriptionBuilder.Subscriptions)
                {
                    Services.TryAddTransient(descriptor.HandlerType);
                }
                Services.AddSingleton(sp =>
                    subscriptionBuilder.Build());
            }
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
