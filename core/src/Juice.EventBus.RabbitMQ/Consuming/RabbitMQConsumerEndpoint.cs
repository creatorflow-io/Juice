namespace Juice.EventBus.RabbitMQ.Consuming
{
    public sealed class RabbitMQConsumerEndpoint
    {
        public string ConnectionName { get; set; } = default!;
        /// <summary>
        /// The name will be used to create a queue for subscribing to messages
        /// </summary>
        public string Queue { get; init; } = default!;
        public ushort QosPrefetchCount { get; private set; } = 10;

        public DeadLetterConfig? DeadLetter { get; set; }

        public void SetQosPrefetchCount(ushort prefetchCount)
        {
            if (prefetchCount <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(prefetchCount), "Prefetch count must be greater than zero.");
            }
            QosPrefetchCount = prefetchCount;
        }
    }

    public class DeadLetterConfig
    {
        /// <summary>
        /// Dead letter exchange name
        /// </summary>
        public string Exchange { get; set; } = string.Empty;

        /// <summary>
        /// Dead letter routing key
        /// </summary>
        public string? RoutingKey { get; set; }

        /// <summary>
        /// Dead letter routing pattern
        /// </summary>
        public string? RoutingPattern { get; set; }

        /// <summary>
        /// Whether to send to DLQ after max retries
        /// </summary>
        public bool Enabled { get; set; } = true;

        public string GetRoutingKey(string originalRoutingKey)
        {
            if (!string.IsNullOrWhiteSpace(RoutingKey))
            {
                return RoutingKey;
            }
            if (!string.IsNullOrWhiteSpace(RoutingPattern))
            {
                return string.Format(RoutingPattern, originalRoutingKey);
            }
            return originalRoutingKey;
        }
    }
}
