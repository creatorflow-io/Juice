using Juice.EventBus.Extensions;
using RabbitMQ.Client;

namespace Juice.EventBus.RabbitMQ.Infrastructure
{
    internal sealed class RabbitMQExchangeDefinition
    {
        public string Name { get; init; } = default!;
        public string Type { get; init; } = ExchangeType.Direct;
        public bool Durable { get; init; } = true;
    }

    internal sealed class RabbitMQQueueDefinition
    {
        public string Name { get; init; } = default!;
        public bool Durable { get; init; } = true;
        public IDictionary<string, object?>? Arguments { get; init; }
    }

    internal sealed class RabbitMQBindingDefinition
    {
        public string Exchange { get; init; } = default!;
        public string Queue { get; init; } = default!;
        public string RoutingKey { get; init; } = default!;
    }

    internal sealed record RabbitMQInfrastructureDefinition(
        IReadOnlyList<RabbitMQExchangeDefinition> Exchanges,
        IReadOnlyList<RabbitMQQueueDefinition> Queues,
        IReadOnlyList<RabbitMQBindingDefinition> Bindings);
}
