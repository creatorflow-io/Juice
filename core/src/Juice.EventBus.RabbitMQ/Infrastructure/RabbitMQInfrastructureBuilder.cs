using Juice.EventBus.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;

namespace Juice.EventBus.RabbitMQ.Infrastructure
{
    public sealed class RabbitMQInfrastructureBuilder
    {
        private readonly List<RabbitMQExchangeDefinition> _exchanges = [];
        private readonly List<RabbitMQQueueDefinition> _queues = [];
        private readonly List<RabbitMQBindingDefinition> _bindings = [];
        private string _connectionName;

        public RabbitMQInfrastructureBuilder(string connectionName)
        {
            _connectionName = connectionName;
        }

        public RabbitMQInfrastructureBuilder DeclareExchange(
            string name,
            string type,
            bool durable = true)
        {
            _exchanges.Add(new RabbitMQExchangeDefinition
            {
                Name = name,
                Type = type,
                Durable = durable,

            });
            return this;
        }

        public RabbitMQInfrastructureBuilder DeclareQueue(
            string name,
            bool durable = true,
            int? ttlMilliseconds = 172800000,
            IDictionary<string, object?>? arguments = null)
        {
            if (arguments == null && ttlMilliseconds.HasValue)
            {
                arguments = RabbitMQTopologyHelper.TTLQueueArguments(ttlMilliseconds.Value);
            }
            else if (arguments != null && !arguments.ContainsKey("x-message-ttl") && ttlMilliseconds.HasValue)
            {
                arguments["x-message-ttl"] = ttlMilliseconds.Value;
            }
            _queues.Add(new RabbitMQQueueDefinition
            {
                Name = name,
                Durable = durable,
                Arguments = arguments
            });
            return this;
        }

        public RabbitMQInfrastructureBuilder BindQueue(
            string queue,
            string exchange,
            string routingKey)
        {
            _bindings.Add(new RabbitMQBindingDefinition
            {
                Exchange = exchange,
                Queue = queue,
                RoutingKey = routingKey
            });
            return this;
        }


        /// ======================= DOMAIN: CONTENT =======================
        ///
        ///                      ┌──────────────────────────────┐
        ///                      │     content.main.exchange    │  (direct)
        ///                      └─────────────┬────────────────┘
        ///                                    │ routingKey = EventName
        ///                                    v
        ///                      ┌──────────────────────────────┐
        ///                      │       content.main.queue     │
        ///                      └─────────────┬────────────────┘
        ///                                    │
        ///                                    │ Retryable failure
        ///                                    v
        /// ======================= RETRY ================================
        ///
        ///                      ┌──────────────────────────────┐
        ///                      │     content.retry.exchange   │  (direct / topic)
        ///                      └─────────────┬────────────────┘
        ///                                    │
        ///           ┌────────────────────────┼────────────────────────┐
        ///           │                        │                        │
        ///       retry.10s                retry.1m                 retry.5m
        ///           │                        │                        │
        ///           v                        v                        v
        /// ┌──────────────────-┐   ┌──────────────────┐   ┌──────────────────┐
        /// │ content.retry.10s │   │ content.retry.1m │   │ content.retry.5m │
        /// │ TTL = 10s         │   │ TTL = 1m         │   │ TTL = 5m         │
        /// │ DLX = content     │   │ DLX = content    │   │ DLX = content    │
        /// └──────────────────-┘   └──────────────────┘   └──────────────────┘
        ///                                    │
        ///                                    │ TTL expired
        ///                                    v
        ///                      ┌──────────────────────────────┐
        ///                      │     content.main.exchange    │
        ///                      └──────────────────────────────┘
        ///
        /// ======================= PARKING ===============================
        ///
        ///  retry-count >= max
        ///         │
        ///         v
        /// ┌──────────────────────────────┐
        /// │  content.parking.exchange    │
        /// └─────────────┬────────────────┘
        ///               v
        /// ┌──────────────────────────────┐
        /// │    content.parking.queue     │
        /// │   (no consumer, manual ops)  │
        /// └──────────────────────────────┘
        /// 
        /// Notes:
        /// - One retry exchange per domain
        /// - Delay is determined by routing key (retry.10s / retry.1m / retry.5m)
        /// - Consumer decides retry tier based on x-retry-count
        /// - Retry queues dead-letter back to domain main exchange
        /// - Parking queue is used for poison / non-retryable messages
        /// 
        public RabbitMQRetryTopologyBuilder DeclareRetryTopology(string mainExchange,
            bool durable = true,
            bool parking = false,
            string? retryExchange = default)
        {
            return new RabbitMQRetryTopologyBuilder(this, mainExchange, retryExchange, durable, parking);
        }

        internal RabbitMQInfrastructureInitializer Build(IServiceProvider sp)
        {
            IRabbitMQPersistentConnection connection = true switch
            {
                _ when !string.IsNullOrEmpty(_connectionName)
                    => sp.GetKeyedService<IRabbitMQPersistentConnection>(_connectionName)
                        ?? throw new InvalidOperationException($"RabbitMQ connection with name '{_connectionName}' is not registered."),
                _ => throw new InvalidOperationException("Infrastructure Connection is required.")
            };

            var logger = sp.GetRequiredService<ILogger<RabbitMQInfrastructureInitializer>>();
            return new RabbitMQInfrastructureInitializer(
                connection: connection,
                definition: new RabbitMQInfrastructureDefinition(
                    Exchanges: _exchanges,
                    Queues: _queues,
                    Bindings: _bindings),
                logger: logger);
        }
    }
}
