using Juice.EventBus.Subscriptions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace Juice.EventBus.RabbitMQ.Consuming
{
    public sealed class RabbitMQConsumerBuilder
    {
        internal record SubscriptionDescriptor(string? Route, Type EventType, Type HandlerType);

        private readonly IServiceCollection _services;
        private readonly EventBusBuilder _eventBus;

        private readonly List<SubscriptionDescriptor> _subscriptions = [];
        private readonly RabbitMQConsumerEndpoint _endpoint;

        private readonly string _serviceKey;

        internal RabbitMQConsumerBuilder(string connectionName, string queue, EventBusBuilder eventBus)
        {
            _endpoint = new() { Queue = queue, ConnectionName = connectionName };
            _serviceKey = $"{connectionName}:{queue}";
            _services = eventBus.Services;
            _eventBus = eventBus;
            AddRequiredServices();
        }

        private void AddRequiredServices()
        {
            _services.TryAddTransient<RabbitMQConsumerEngine>();
            _eventBus.AddConsumerServices(_serviceKey);
        }

        public RabbitMQConsumerBuilder ConfigureQos(ushort prefetchCount)
        {
            _endpoint.SetQosPrefetchCount(prefetchCount);
            return this;
        }

        public RabbitMQConsumerBuilder WithDeadLetterExchange(string exchange, string? routingKey = null, string? routingPattern = "{0}.parking")
        {
            _endpoint.DeadLetter = new DeadLetterConfig
            {
                Enabled = true,
                Exchange = exchange,
                RoutingKey = routingKey,
                RoutingPattern = routingPattern
            };
            return this;
        }

        /// <summary>
        /// Subscribe to an integration event with a specific handler for this consumer.
        /// </summary>
        /// <typeparam name="TEvent"></typeparam>
        /// <typeparam name="THandler"></typeparam>
        /// <param name="route"></param>
        /// <returns></returns>
        public RabbitMQConsumerBuilder Subscribe<TEvent, THandler>(string? route = default)
            where TEvent : IIntegrationEvent
            where THandler : class, IIntegrationEventHandler<TEvent>
        {
            _subscriptions.Add(new SubscriptionDescriptor(route, typeof(TEvent), typeof(THandler)));
            _services.TryAddTransient<THandler>();
            return this;
        }

        internal RabbitMQConsumerHostedService BuildHostedService(IServiceProvider sp)
        {
            if (string.IsNullOrWhiteSpace(_endpoint.Queue))
                throw new InvalidOperationException($"Consumer Queue is required.");
            if (string.IsNullOrWhiteSpace(_endpoint.ConnectionName))
                throw new InvalidOperationException($"Consumer Connection is required.");

            var subsManager = sp.GetRequiredKeyedService<ISubscriptionsManager>(_serviceKey);

            foreach (var descriptor in _subscriptions)
            {
                subsManager.AddSubscription(descriptor.EventType, descriptor.HandlerType, descriptor.Route);
            }
            var engine = sp.GetRequiredService<RabbitMQConsumerEngine>();
            var logger = sp.GetRequiredService<ILogger<RabbitMQConsumerHostedService>>();

            return new RabbitMQConsumerHostedService(engine, _endpoint, subsManager, logger);
        }

    }
}
