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

        /// <summary>
        /// Dead letter routing configuration
        /// </summary>
        public string? DLRoutingPattern { get; set; }
        public void SetQosPrefetchCount(ushort prefetchCount)
        {
            if (prefetchCount <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(prefetchCount), "Prefetch count must be greater than zero.");
            }
            QosPrefetchCount = prefetchCount;
        }
        public void SetDLRoutingPattern(string pattern)
        {
            if (string.IsNullOrWhiteSpace(pattern))
            {
                throw new ArgumentException("DL routing pattern cannot be null or whitespace.", nameof(pattern));
            }
            DLRoutingPattern = pattern;
        }
    }
}
