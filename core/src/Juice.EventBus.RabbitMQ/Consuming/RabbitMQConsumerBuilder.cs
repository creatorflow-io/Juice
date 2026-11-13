using Juice.EventBus.Subscriptions;
using Juice.Messaging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace Juice.EventBus.RabbitMQ.Consuming
{
    public sealed class RabbitMQConsumerBuilder : IConsumerBuilder
    {
        internal record SubscriptionDescriptor(string? Route, Type EventType, Type HandlerType);

        private readonly IServiceCollection _services;
        private readonly EventBusBuilder _eventBus;

        private readonly List<SubscriptionDescriptor> _subscriptions = [];
        private readonly RabbitMQConsumerEndpoint _endpoint;

        private readonly string _serviceKey;

        internal RabbitMQConsumerBuilder(string key, string connectionName, string queue, EventBusBuilder eventBus)
        {
            _endpoint = new() { Queue = queue, ConnectionName = connectionName };
            _serviceKey = key;
            _services = eventBus.Services;
            _eventBus = eventBus;
            AddRequiredServices();
        }

        private void AddRequiredServices()
        {
            if (_services.Any(d => d.ServiceType == typeof(RabbitMQConsumerStatus) && Equals(d.ServiceKey, _serviceKey)))
            {
                // the key also names the consumer's subscriptions: two consumers under one key would share them
                throw new InvalidOperationException($"A RabbitMQ consumer with the key '{_serviceKey}' has already been registered.");
            }
            _services.AddKeyedSingleton(_serviceKey, new RabbitMQConsumerStatus(_serviceKey, _endpoint.Queue));
            _services.TryAddTransient<RabbitMQConsumerEngine>();
            _eventBus.AddConsumerServices(_serviceKey);
        }

        /// <summary>How many messages the broker sends ahead, unacknowledged (10 by default).</summary>
        public RabbitMQConsumerBuilder ConfigureQos(ushort prefetchCount)
        {
            _endpoint.SetQosPrefetchCount(prefetchCount);
            return this;
        }

        /// <summary>
        /// For a queue whose retries the broker makes (its <c>x-dead-letter-exchange</c> routes a rejected message back to it, through a
        /// delay queue) rather than a retry policy: a message that failed <paramref name="attempts"/> times, counted from the broker's
        /// <c>x-death</c> record of its rejections from this queue, is moved to <paramref name="exchange"/> and acknowledged instead of
        /// rejected again. A message that can't be read is moved at once. The exchange, and a queue bound to it, are the application's
        /// to declare; the routing key is the dead-letter routing pattern (<c>{0}.parking</c>) applied to the message's routing key.
        /// <para>The moved copy keeps the message's headers, with its <c>x-death</c> history, but not its <c>user_id</c>: the broker accepts
        /// that only from the publisher's own connection. A retry policy covering the queue's source takes precedence.</para>
        /// </summary>
        public RabbitMQConsumerBuilder ParkAfter(int attempts, string exchange)
        {
            _endpoint.SetParking(attempts, exchange);
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

        IConsumerBuilder IConsumerBuilder.Subscribe<TEvent, THandler>(string? route)
            => Subscribe<TEvent, THandler>(route);

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
            var status = sp.GetRequiredKeyedService<RabbitMQConsumerStatus>(_serviceKey);
            var logger = sp.GetRequiredService<ILogger<RabbitMQConsumerHostedService>>();

            return new RabbitMQConsumerHostedService(_serviceKey, engine, _endpoint, subsManager, status, logger);
        }

    }
}
