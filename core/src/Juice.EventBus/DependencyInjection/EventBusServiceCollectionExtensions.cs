using Juice.EventBus;
using Juice.EventBus.Internal;
using Juice.EventBus.Policies;
using Juice.EventBus.Subscriptions;
using Juice.Messaging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;


namespace Microsoft.Extensions.DependencyInjection
{
    public static class EventBusServiceCollectionExtensions
    {
        public static EventBusBuilder AddEventBus(this IServiceCollection services, Action<EventBusBuilder>? configure = default)
        {
            var builder = new EventBusBuilder(services, default);
            configure?.Invoke(builder);
            builder.AddDefaultServices();
            return builder;
        }

        public static EventBusBuilder AddEventBus(this MessagingBuilder messagingBuilder, Action<EventBusBuilder>? configure = default)
        {
            var builder = new EventBusBuilder(messagingBuilder.Services, messagingBuilder);
            configure?.Invoke(builder);
            builder.AddDefaultServices();
            return builder;
        }

        /// <summary>
        /// Registers the keyed <see cref="ISubscriptionsManager"/> (key <c>"local"</c>) for
        /// in-process dispatch routes (<c>"local"</c> and <c>"local-channel"</c>).
        /// The manager is populated from all <see cref="ILocalSubscriptionsProvider"/> instances
        /// registered in the container (added by <c>AddLocalConsumer</c>).
        /// Safe to call multiple times — subsequent calls are no-ops.
        /// </summary>
        public static IServiceCollection AddLocalSubscriptionsManager(this IServiceCollection services)
        {
            services.TryAddKeyedSingleton<ISubscriptionsManager>("local", (sp, k) =>
            {
                var logger = sp.GetRequiredService<ILoggerFactory>()
                    .CreateLogger(typeof(InMemorySubscriptionsManager).Name + "[local]");
                var providers = sp.GetServices<ILocalSubscriptionsProvider>()
                    .Cast<ISubscriptionsProvider>();
                return new InMemorySubscriptionsManager(providers, logger, topicSupport: true);
            });
            return services;
        }
    }

    public class EventBusBuilder
    {
        public MessagingBuilder Messaging { get; }
        public IServiceCollection Services => _services;
        private readonly IServiceCollection _services;
        internal EventBusBuilder(IServiceCollection services, MessagingBuilder? messaging)
        {
            Messaging = messaging ?? services.AddMessaging();
            _services = services;
        }

        public EventBusBuilder AddDefaultServices()
        {
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

        /// <summary>
        /// Add consumer retry policies from configuration section.
        /// </summary>
        /// <param name="policies"></param>
        /// <returns></returns>
        public EventBusBuilder AddConsumerRetryPolicies(IConfigurationSection policies)
        {
            Services.Configure<ConsumeRetryPolicyOptions>(policies);
            return this;
        }
        public EventBusBuilder AddConsumerRetryPolicies(Action<ConsumeRetryPolicyOptions> configure)
        {
            Services.Configure(configure);
            return this;
        }
        #endregion

        #region Producer services

        /// <summary>
        /// Add <see cref="IEventBus"/> implementation for event publishing.
        /// </summary>
        /// <returns></returns>
        public EventBusBuilder AddPublishingServices()
        {
            _services.TryAddSingleton<IEventBus, CompositeEventPublisher>();

            return this;
        }

        #endregion
    }
}
