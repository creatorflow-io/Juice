using Juice.EventBus.Delivery.Policies;
using Juice.EventBus.Delivery.Policies.Internal;
using Juice.EventBus.Delivery.Processing;
using Juice.EventBus.Registry;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace Juice.EventBus.Delivery
{
    public sealed class DeliveryBuilder
    {
        private readonly IServiceCollection _services;
        public IServiceCollection Services => _services;
        private readonly HashSet<Type> _registeredTypes = new();

        public DeliveryBuilder(IServiceCollection services)
        {
            _services = services;
        }

        /// <summary>
        /// Adds and configures delivery processing for the specified publisher and context.
        /// </summary>
        /// <typeparam name="TContext"></typeparam>
        /// <param name="publisher"></param>
        /// <param name="configure"></param>
        /// <returns></returns>
        public DeliveryBuilder AddDeliveryProcessor<TContext>(string publisher, Action<DeliveryProcessorBuilder>? configure = default)
        {
            var builder = new DeliveryProcessorBuilder(publisher, _services);

            // ✅ Register default intents automatically if not configured
            builder.WithDefaultIntents();

            configure?.Invoke(builder);

            _services.AddHostedService(sp => builder.BuildHostedService<TContext>(sp));
            return this;
        }

        /// <summary>
        /// Adds and configures delivery processing for the specified publisher and context.
        /// </summary>
        /// <typeparam name="TContext"></typeparam>
        /// <param name="publisher"></param>
        /// <param name="intents"></param>
        /// <returns></returns>
        public DeliveryBuilder AddDeliveryProcessor<TContext>(string publisher, params string[] intents)
        {
            var builder = new DeliveryProcessorBuilder(publisher, _services);

            // ✅ Register default intents automatically if not configured
            builder.WithIntents(intents);

            _services.AddHostedService(sp => builder.BuildHostedService<TContext>(sp));
            return this;
        }

        /// <summary>
        /// Adds delivery policies from configuration.
        /// </summary>
        /// <param name="policies"></param>
        /// <returns></returns>
        public DeliveryBuilder AddDeliveryPolicies(IConfigurationSection policies)
        {
            _services.Configure<DeliveryPolicyOptions>(policies);
            _services.TryAddEnumerable(ServiceDescriptor.Singleton<IDeliveryPolicyProvider, DeliveryPolicyConfiguration>());
            _services.TryAddSingleton<IDeliveryPolicyResolver, DefaultDeliveryPolicyResolver>();
            return this;
        }

        /// <summary>
        /// Configure event type registry for delivery.
        /// </summary>
        /// <param name="configure"></param>
        /// <returns></returns>
        public DeliveryBuilder ConfigureEventTypeRegistry(Action<EventTypeRegistryBuilder> configure)
        {
            var registryBuilder = new EventTypeRegistryBuilder();
            configure(registryBuilder);
            _registeredTypes.UnionWith(registryBuilder.EventTypes);
            return this;
        }

        internal void BuildEventTypeRegistry()
        {
            if (_services.Any(p => p.ServiceType == typeof(IEventTypeRegistry)))
            {
                var service = _services.BuildServiceProvider().GetRequiredService<IEventTypeRegistry>();
                service.Merge([.. _registeredTypes]);
            }
            else
            {
                _services.AddSingleton<IEventTypeRegistry>(sp => 
                {
                    var logger = sp.GetRequiredService<ILogger<EventTypeRegistry>>();
                    return new EventTypeRegistry([.. _registeredTypes], logger);
                });
            }
        }
    }
}
