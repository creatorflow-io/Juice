namespace Juice.Messaging.Outbox.Delivery
{
    public sealed record DeliveryPolicy(
        TimeSpan Interval, int BatchSize, TimeSpan Timeout,
        TimeSpan InitialRetryDelay, double RetryDelayMultiplier, int MaxRetryAttempts)
    {
        public static readonly DeliveryPolicy Default = new(
            Interval: TimeSpan.FromSeconds(5),
            BatchSize: 10,
            Timeout: TimeSpan.FromMinutes(5),
            InitialRetryDelay: TimeSpan.FromSeconds(5),
            RetryDelayMultiplier: 2.0,
            MaxRetryAttempts: 3);

        public DateTimeOffset? GetNextAttempt(int retryCount)
        {
            if (retryCount <= 0 || retryCount > MaxRetryAttempts)
            {
                return null;
            }
            var delay = TimeSpan.FromTicks((long)(InitialRetryDelay.Ticks * Math.Pow(RetryDelayMultiplier, retryCount - 1)));
            return DateTimeOffset.UtcNow.Add(delay);
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
