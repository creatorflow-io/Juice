using Juice.Messaging.Outbox.Delivery.Internal;
using Juice.Messaging.Outbox.Delivery.Processing;
using Juice.Messaging.Outbox.Delivery.Registry;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace Juice.Messaging.Outbox.Delivery
{
    public sealed class DeliveryBuilder
    {
        private readonly IServiceCollection _services;
        public IServiceCollection Services => _services;
        private readonly HashSet<Type> _registeredTypes = new();
        public EventBusBuilder EventBus { get; init; }

        public DeliveryBuilder(IServiceCollection services)
        {
            _services = services;
            EventBus = services.AddEventBus();
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

            builder.RegisterProcessorPolicies<TContext>();

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

            builder.RegisterProcessorPolicies<TContext>();

            _services.AddHostedService(sp => builder.BuildHostedService<TContext>(sp));
            return this;
        }


        /// <summary>
        /// Configure event type registry for delivery.
        /// </summary>
        /// <param name="configure"></param>
        /// <returns></returns>
        //public DeliveryBuilder ConfigureEventTypeRegistry(Action<EventTypeRegistryBuilder> configure)
        //{
        //    var registryBuilder = new EventTypeRegistryBuilder();
        //    configure(registryBuilder);
        //    _registeredTypes.UnionWith(registryBuilder.EventTypes);
        //    return this;
        //}


        /// <summary>
        /// Adds delivery policies from configuration.
        /// Safe to call multiple times — each call's policies are merged into the shared options.
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
        /// Adds delivery policies programmatically via a delegate.
        /// Safe to call multiple times — each call's policies are merged into the shared options.
        /// </summary>
        /// <param name="configure"></param>
        /// <returns></returns>
        public DeliveryBuilder AddDeliveryPolicies(Action<DeliveryPolicyOptions> configure)
        {
            _services.Configure<DeliveryPolicyOptions>(configure);
            _services.TryAddEnumerable(ServiceDescriptor.Singleton<IDeliveryPolicyProvider, DeliveryPolicyConfiguration>());
            _services.TryAddSingleton<IDeliveryPolicyResolver, DefaultDeliveryPolicyResolver>();
            return this;
        }

        internal void RegisterEventTypeRegistry()
        {
            if (_services.Any(p => p.ServiceType == typeof(IEventTypeRegistry)))
            {
                if(_registeredTypes.Count == 0)
                    return;
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
