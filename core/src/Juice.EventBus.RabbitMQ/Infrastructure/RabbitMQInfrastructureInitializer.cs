using Microsoft.Extensions.Logging;

namespace Juice.EventBus.RabbitMQ.Infrastructure
{
    internal sealed class RabbitMQInfrastructureInitializer
    {
        private readonly IRabbitMQPersistentConnection _connection;
        private readonly RabbitMQInfrastructureDefinition _definition;
        private readonly ILogger<RabbitMQInfrastructureInitializer> _logger;

        public RabbitMQInfrastructureInitializer(
            IRabbitMQPersistentConnection connection,
            RabbitMQInfrastructureDefinition definition,
            ILogger<RabbitMQInfrastructureInitializer> logger)
        {
            _connection = connection;
            _definition = definition;
            _logger = logger;
        }

        public async Task InitializeAsync(CancellationToken ct = default)
        {
            if (!_connection.IsConnected)
               await _connection.TryConnectAsync(ct).ConfigureAwait(false);

            using var channel = await _connection.CreateChannelAsync(ct);
            if(channel == null)
            {
                _logger.LogError("Failed to create RabbitMQ channel for infrastructure initialization.");
                throw new InvalidOperationException("Could not create RabbitMQ channel.");
            }
            foreach (var ex in _definition.Exchanges)
            {
                await channel.ExchangeDeclareAsync(
                    exchange: ex.Name,
                    type: ex.Type,
                    durable: ex.Durable,
                    autoDelete: false);

                _logger.LogInformation("Declared exchange {Exchange}", ex.Name);
            }

            foreach (var q in _definition.Queues)
            {
                await channel.QueueDeclareAsync(
                    queue: q.Name,
                    durable: q.Durable,
                    exclusive: false,
                    autoDelete: false,
                    arguments: q.Arguments);

                _logger.LogInformation("Declared queue {Queue}", q.Name);
            }

            foreach (var b in _definition.Bindings)
            {
                await channel.QueueBindAsync(
                    queue: b.Queue,
                    exchange: b.Exchange,
                    routingKey: b.RoutingKey);

                _logger.LogInformation(
                    "Bound queue {Queue} to exchange {Exchange} ({RoutingKey})",
                    b.Queue, b.Exchange, b.RoutingKey);
            }

        }
    }
}
