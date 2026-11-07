namespace Juice.Messaging.Outbox.Delivery
{
    public sealed record DeliveryPolicy(
        TimeSpan Interval, int BatchSize, TimeSpan Timeout,
        TimeSpan InitialRetryDelay, double RetryDelayMultiplier, int MaxRetryAttempts)
    {
        /// <summary>
        /// Retries 20 times from 5 s, doubling up to <see cref="MaxRetryDelay"/> (5 min): about 75 minutes before a delivery is
        /// left <c>Failed</c>, enough to ride out a database restart or a consumer deploy.
        /// </summary>
        public static readonly DeliveryPolicy Default = new(
            Interval: TimeSpan.FromSeconds(5),
            BatchSize: 10,
            Timeout: TimeSpan.FromMinutes(5),
            InitialRetryDelay: TimeSpan.FromSeconds(5),
            RetryDelayMultiplier: 2.0,
            MaxRetryAttempts: 20);

        /// <summary>
        /// The longest wait between two attempts of a delivery, so a long <see cref="MaxRetryAttempts"/> keeps retrying at this
        /// pace instead of waiting hours or days.
        /// </summary>
        public TimeSpan MaxRetryDelay { get; init; } = TimeSpan.FromMinutes(5);

        /// <summary>
        /// How many transactions' messages to send at the same time, on a publisher that supports it. Messages of one
        /// transaction are always sent in order. 1 sends every message in order, at a lower rate.
        /// </summary>
        public int MaxConcurrency { get; init; } = 10;

        public DateTimeOffset? GetNextAttempt(int retryCount)
        {
            if (retryCount <= 0 || retryCount > MaxRetryAttempts)
            {
                return null;
            }
            return DateTimeOffset.UtcNow.Add(GetRetryDelay(retryCount));
        }

        /// <summary>The wait before retry <paramref name="retryCount"/> (1-based), capped at <see cref="MaxRetryDelay"/>.</summary>
        public TimeSpan GetRetryDelay(int retryCount)
        {
            // computed in double: Math.Pow overflows long ticks after a few dozen doublings
            var ticks = InitialRetryDelay.Ticks * Math.Pow(RetryDelayMultiplier, Math.Max(retryCount - 1, 0));
            return ticks >= MaxRetryDelay.Ticks ? MaxRetryDelay : TimeSpan.FromTicks((long)ticks);
        }

        public TimeSpan GetBackoffDelay(int failureCount, TimeSpan? maxDelay = default)
        {
            // Exponential backoff: InitialRetryDelay * (RetryDelayMultiplier ^ (failureCount - 1))
            var delay = TimeSpan.FromTicks((long)(InitialRetryDelay.Ticks * Math.Pow(RetryDelayMultiplier, failureCount - 1)));
            // Cap the delay to a maximum of 5 minutes
            var max = maxDelay ?? TimeSpan.FromMinutes(5);
            return delay < max ? delay : max;
        }
    }
}
