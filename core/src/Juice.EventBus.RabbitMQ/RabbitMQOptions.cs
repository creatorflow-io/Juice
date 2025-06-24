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
        /// <summary>
        /// Indicates whether to acknowledge messages after they have been processed or must succeeded.
        /// <para>Default <c>true</c> if the value is not set</para>
        /// </summary>
        public bool? AckOnProcessed { get; set; }
    }
}
