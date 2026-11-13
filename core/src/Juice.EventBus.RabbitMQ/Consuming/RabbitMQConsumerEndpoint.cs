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

        /// <summary>
        /// Where a message goes when no retry policy covers it and it keeps failing (<see cref="ParkAfterAttempts"/>), or can't be read: an
        /// exchange the application declares. Null: such a message is rejected, which a queue's <c>x-dead-letter-exchange</c> may route back
        /// to it for ever.
        /// </summary>
        public string? ParkingExchange { get; private set; }

        /// <summary>How many failed attempts a message gets, counted from the broker's own dead-lettering of it (<c>x-death</c>), before it is parked.</summary>
        public int ParkAfterAttempts { get; private set; }

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

        public void SetParking(int attempts, string exchange)
        {
            if (attempts <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(attempts), "A message gets at least one attempt before it is parked.");
            }
            if (string.IsNullOrWhiteSpace(exchange))
            {
                throw new ArgumentException("The parking exchange is required.", nameof(exchange));
            }
            ParkAfterAttempts = attempts;
            ParkingExchange = exchange;
        }
    }
}
