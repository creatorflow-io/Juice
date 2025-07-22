namespace Juice.EventBus.RabbitMQ
{
    public class RabbitMQOptions : EventBusOptions
    {
        /// <summary>
        /// Will be used to create a queue for subscribing to messages.
        /// </summary>
        public string? SubscriptionClientName { get; set; }
        public bool RabbitMQEnabled { get; set; }
        public int Port { get; set; }
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
        public string VirtualHost { get; set; }
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
        /// <summary>
        /// Exchange name to use for publishing messages.
        /// </summary>
        public string? BrokerName { get; set; }
        /// <summary>
        /// Exchange type to use for publishing messages. (EX: direct, fanout, topic, headers)
        /// </summary>
        public string? ExchangeType { get; set; }
        /// <summary>
        /// Queue type to use for subscribing to messages. (EX: classic, quorum)
        /// </summary>
        public string? QueueType { get; set; }
        public long TTL { get; set; } = 172800000; // Time to live in milliseconds for messages in the queue. Default is 48 hours (48 * 60 * 60 * 1000 ms).
        /// <summary>
        /// Maximum number of retries for processing a message before it is considered failed.
        /// <para>No retry if less than or equal 0</para>
        /// </summary>
        public int ProcessMaxRetries { get; set; }
        /// <summary>
        /// Retry message after milliseconds, use when declare DLX queue
        /// </summary>
        public int ProcessRetryDelayMs { get; set; } = 5000;
    }
}
