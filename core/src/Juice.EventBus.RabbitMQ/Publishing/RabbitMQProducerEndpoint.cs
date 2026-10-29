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
        /// Maximum number of retry attempts for publishing a message when the connection or channel drops. Default is 5.
        /// <para>Only dropped connections and channels are retried, with an exponential delay starting at
        /// <see cref="RetryBaseDelay"/> (0.2s, 0.4s, 0.8s... by default). Refusals by the broker (unroutable, nack, missing
        /// exchange) fail at once and are retried by the outbox delivery policy.</para>
        /// </summary>
        public int MaxRetryAttempts { get; private set; } = 5;

        /// <summary>
        /// The delay before the first publish retry; each next retry waits twice as long. Default is 200 ms.
        /// </summary>
        public TimeSpan RetryBaseDelay { get; private set; } = TimeSpan.FromMilliseconds(200);

        /// <summary>
        /// Publish with the <c>mandatory</c> flag: a message that no queue is bound for fails the publish (the outbox
        /// records the delivery as failed and retries it). Off by default: publishing an event nobody subscribes to yet
        /// is normal pub/sub, and the broker drops it. Turn it on for exchanges that must always have a consumer.
        /// </summary>
        public bool Mandatory { get; private set; }

        public void SetMandatory(bool mandatory)
        {
            Mandatory = mandatory;
        }

        public void SetRetryBaseDelay(TimeSpan delay)
        {
            if (delay < TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(delay), "Retry delay must not be negative.");
            }
            RetryBaseDelay = delay;
        }

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
