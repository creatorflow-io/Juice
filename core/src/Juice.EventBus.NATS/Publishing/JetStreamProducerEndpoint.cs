namespace Juice.EventBus.NATS.Publishing
{
    public class JetStreamProducerEndpoint
    {
        public string Key { get; set; } = default!;
        /// <summary>
        /// Subject prefix used when <c>PublishContext.Destination</c> is not set. Default is <c>default_exchange</c>.
        /// </summary>
        public string DefaultDestination { get; private set; } = "default_exchange";
        /// <summary>
        /// Client-level retry attempts when no stream responds yet (e.g. during a leader election). Default is 2.
        /// </summary>
        public int MaxRetryAttempts { get; private set; } = 2;
        /// <summary>
        /// Time to wait for the stream acknowledgement. Default is 5 seconds.
        /// </summary>
        public TimeSpan PublishTimeout { get; private set; } = TimeSpan.FromSeconds(5);

        public void SetDefaultDestination(string destination)
        {
            JetStreamNaming.ValidateToken(destination, nameof(destination));
            DefaultDestination = destination;
        }

        public void SetMaxRetryAttempts(ushort maxRetryAttempts)
        {
            MaxRetryAttempts = maxRetryAttempts;
        }

        public void SetPublishTimeout(TimeSpan timeout)
        {
            if (timeout <= TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(timeout), "Publish timeout must be greater than zero.");
            }
            PublishTimeout = timeout;
        }
    }
}
