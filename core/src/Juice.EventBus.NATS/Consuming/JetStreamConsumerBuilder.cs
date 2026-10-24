using Juice.EventBus.NATS.Infrastructure;
using Juice.EventBus.Subscriptions;
using Juice.Messaging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace Juice.EventBus.NATS.Consuming
{
    public sealed class JetStreamConsumerBuilder : IConsumerBuilder
    {
        internal record SubscriptionDescriptor(string? Route, Type EventType, Type HandlerType);

        private readonly IServiceCollection _services;
        private readonly EventBusBuilder _eventBus;

        private readonly List<SubscriptionDescriptor> _subscriptions = [];
        private readonly JetStreamConsumerEndpoint _endpoint;

        private readonly string _serviceKey;

        internal JetStreamConsumerBuilder(string key, string stream, string durableName, string connectionName, EventBusBuilder eventBus)
        {
            _endpoint = new() { Key = key, Stream = stream, DurableName = durableName, ConnectionName = connectionName };
            _serviceKey = key;
            _services = eventBus.Services;
            _eventBus = eventBus;
            AddRequiredServices();
        }

        private void AddRequiredServices()
        {
            _services.TryAddTransient<JetStreamConsumerEngine>();
            _eventBus.AddConsumerServices(_serviceKey);
        }

        /// <summary>
        /// Subject prefix the stream captures (the publishing destination). Required.
        /// </summary>
        public JetStreamConsumerBuilder Destination(string destination)
        {
            _endpoint.SetDestination(destination);
            return this;
        }

        public JetStreamConsumerBuilder MaxConcurrency(int value)
        {
            _endpoint.SetMaxConcurrency(value);
            return this;
        }

        public JetStreamConsumerBuilder AckWait(TimeSpan value)
        {
            _endpoint.SetAckWait(value);
            return this;
        }

        public JetStreamConsumerBuilder MaxDeliver(int value)
        {
            _endpoint.SetMaxDeliver(value);
            return this;
        }

        public JetStreamConsumerBuilder ParkingPrefix(string prefix)
        {
            _endpoint.SetParkingPrefix(prefix);
            return this;
        }

        public JetStreamConsumerBuilder OnNotHandled(NotHandledBehavior behavior)
        {
            _endpoint.SetNotHandledBehavior(behavior);
            return this;
        }

        public JetStreamConsumerBuilder Provisioning(JetStreamProvisioning mode)
        {
            _endpoint.SetProvisioning(mode);
            return this;
        }

        /// <summary>
        /// Subscribe to an integration event with a specific handler for this consumer.
        /// </summary>
        /// <typeparam name="TEvent"></typeparam>
        /// <typeparam name="THandler"></typeparam>
        /// <param name="route">RabbitMQ-style topic route (<c>*</c>, trailing <c>#</c>); defaults to the event type name</param>
        /// <returns></returns>
        public JetStreamConsumerBuilder Subscribe<TEvent, THandler>(string? route = default)
            where TEvent : IIntegrationEvent
            where THandler : class, IIntegrationEventHandler<TEvent>
        {
            _subscriptions.Add(new SubscriptionDescriptor(route, typeof(TEvent), typeof(THandler)));
            _services.TryAddTransient<THandler>();
            return this;
        }

        IConsumerBuilder IConsumerBuilder.Subscribe<TEvent, THandler>(string? route)
            => Subscribe<TEvent, THandler>(route);

        internal IReadOnlyList<string> BuildFilterSubjects()
        {
            var filters = _subscriptions
                .Select(s => JetStreamNaming.ToFilterSubject(_endpoint.Destination, s.Route ?? s.EventType.Name))
                .Distinct()
                .ToList();
            var all = $"{_endpoint.Destination}.>";
            if (filters.Count == 0 || filters.Contains(all))
            {
                return [all];
            }
            // the server rejects overlapping filters: drop the ones another filter already covers
            filters = filters
                .Where(f => !filters.Any(other => other != f && Covers(other, f)))
                .ToList();
            // partial overlaps cannot be split; widen like a non-trailing '#' and leave the exact
            // match to the topic matching of the subscriptions manager
            for (var i = 0; i < filters.Count; i++)
            {
                for (var j = i + 1; j < filters.Count; j++)
                {
                    if (JetStreamInfrastructureBuilder.SubjectsOverlap(filters[i], filters[j]))
                    {
                        return [all];
                    }
                }
            }
            return filters;
        }

        /// <summary>
        /// True when every subject matched by <paramref name="filter"/> is also matched by <paramref name="cover"/>.
        /// </summary>
        internal static bool Covers(string cover, string filter)
        {
            var tc = cover.Split('.');
            var tf = filter.Split('.');
            for (var i = 0; i < tc.Length; i++)
            {
                if (i >= tf.Length)
                {
                    return false;
                }
                if (tc[i] == ">")
                {
                    return true;
                }
                if (tf[i] == ">")
                {
                    return false;
                }
                if (tc[i] != "*" && (tf[i] == "*" || tc[i] != tf[i]))
                {
                    return false;
                }
            }
            return tc.Length == tf.Length;
        }

        internal JetStreamConsumerHostedService BuildHostedService(IServiceProvider sp)
        {
            if (string.IsNullOrWhiteSpace(_endpoint.Stream))
                throw new InvalidOperationException($"Consumer Stream is required.");
            if (string.IsNullOrWhiteSpace(_endpoint.DurableName))
                throw new InvalidOperationException($"Consumer DurableName is required.");
            if (string.IsNullOrWhiteSpace(_endpoint.ConnectionName))
                throw new InvalidOperationException($"Consumer Connection is required.");
            if (string.IsNullOrWhiteSpace(_endpoint.Destination))
                throw new InvalidOperationException($"Consumer Destination is required for '{_endpoint.Stream}/{_endpoint.DurableName}'. Call Destination(...).");

            var subsManager = sp.GetRequiredKeyedService<ISubscriptionsManager>(_serviceKey);

            foreach (var descriptor in _subscriptions)
            {
                subsManager.AddSubscription(descriptor.EventType, descriptor.HandlerType, descriptor.Route);
            }
            _endpoint.FilterSubjects = BuildFilterSubjects();

            var engine = sp.GetRequiredService<JetStreamConsumerEngine>();
            var logger = sp.GetRequiredService<ILogger<JetStreamConsumerHostedService>>();

            return new JetStreamConsumerHostedService(_serviceKey, engine, _endpoint, subsManager, logger);
        }
    }
}
