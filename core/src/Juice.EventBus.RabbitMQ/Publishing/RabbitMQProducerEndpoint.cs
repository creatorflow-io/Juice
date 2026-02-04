namespace Juice.EventBus.RabbitMQ.Publishing
{
    public class RabbitMQProducerEndpoint
    {
        public string Key { get; set; } = default!;
        /// <summary>
        /// Default exchange name to publish messages to.
        /// </summary>
        public string? DefaultExchange { get; private set; }
        /// <summary>
        /// Maximum number of retry attempts for publishing a message. Default is 5.
        /// </summary>
        public int MaxRetryAttempts { get; private set; } = 5;

        /// <summary>
        /// The capacity of the channel pool. Default is 10.
        /// </summary>
        public int PoolCapacity { get; private set; } = 10;

        public void SetMaxRetryAttempts(ushort maxRetryAttempts)
        {
            MaxRetryAttempts = maxRetryAttempts;
        }

        public void SetDefaultExchange(string exchange)
        {
            DefaultExchange = exchange;
        }

        public void SetPoolCapacity(int capacity)
        {
            if (capacity <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(capacity), "Pool capacity must be greater than zero.");
            }
            PoolCapacity = capacity;
        }
    }
}
