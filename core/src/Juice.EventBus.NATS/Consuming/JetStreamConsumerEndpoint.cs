using Juice.EventBus.NATS.Infrastructure;

namespace Juice.EventBus.NATS.Consuming
{
    public sealed class JetStreamConsumerEndpoint
    {
        /// <summary>
        /// Subscriptions-manager key; also the MessageContext source of dispatched messages.
        /// </summary>
        public string Key { get; init; } = default!;
        public string ConnectionName { get; init; } = default!;
        /// <summary>
        /// The stream that holds the messages.
        /// </summary>
        public string Stream { get; init; } = default!;
        /// <summary>
        /// The durable consumer name. Instances sharing it share the work.
        /// </summary>
        public string DurableName { get; init; } = default!;
        /// <summary>
        /// Subject prefix the stream captures. Used to derive filter subjects and as the retry policy source.
        /// </summary>
        public string Destination { get; private set; } = default!;
        /// <summary>
        /// Maximum number of messages handled at the same time. Default is 10.
        /// </summary>
        public int MaxConcurrency { get; private set; } = 10;
        /// <summary>
        /// Time the server waits for an acknowledgement before redelivering. Default is 30 seconds.
        /// Long-running handlers send progress every <c>AckWait / 2</c>.
        /// </summary>
        public TimeSpan AckWait { get; private set; } = TimeSpan.FromSeconds(30);
        /// <summary>
        /// Server-side delivery limit. Default is -1 (unlimited; retries are bounded by the retry policy).
        /// </summary>
        public int MaxDeliver { get; private set; } = -1;
        /// <summary>
        /// Parking subject prefix used when the retry policy has no <c>DeadLetterDest</c>.
        /// Default is <c>parking.{Destination}</c>.
        /// </summary>
        public string? ParkingPrefix { get; private set; }
        public NotHandledBehavior NotHandledBehavior { get; private set; } = NotHandledBehavior.Terminate;
        public JetStreamProvisioning Provisioning { get; private set; } = JetStreamProvisioning.VerifyOnly;
        /// <summary>
        /// Filter subjects computed from the subscriptions.
        /// </summary>
        public IReadOnlyList<string> FilterSubjects { get; internal set; } = [];

        public void SetDestination(string destination)
        {
            JetStreamNaming.ValidateToken(destination, nameof(destination));
            Destination = destination;
        }

        public void SetMaxConcurrency(int value)
        {
            if (value < 1 || value > 1000)
            {
                throw new ArgumentOutOfRangeException(nameof(value), "Max concurrency must be between 1 and 1000.");
            }
            MaxConcurrency = value;
        }

        public void SetAckWait(TimeSpan value)
        {
            if (value < TimeSpan.FromSeconds(1))
            {
                throw new ArgumentOutOfRangeException(nameof(value), "AckWait must be at least 1 second.");
            }
            AckWait = value;
        }

        public void SetMaxDeliver(int value)
        {
            if (value != -1 && value <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(value), "MaxDeliver must be -1 (unlimited) or greater than zero.");
            }
            MaxDeliver = value;
        }

        public void SetParkingPrefix(string prefix)
        {
            JetStreamNaming.ValidateToken(prefix, nameof(prefix));
            ParkingPrefix = prefix;
        }

        public void SetNotHandledBehavior(NotHandledBehavior behavior)
        {
            NotHandledBehavior = behavior;
        }

        public void SetProvisioning(JetStreamProvisioning mode)
        {
            Provisioning = mode;
        }
    }
}
