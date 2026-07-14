namespace Juice.Messaging.Idempotency
{
    /// <summary>
    /// Shared configuration for the idempotency subsystem, consumed by every
    /// <see cref="IIdempotencyService"/> store (InMemory, DistributedCache, Redis, EF)
    /// and by the ASP.NET Core HTTP idempotency layer.
    /// <para></para>
    /// Replaces the previously hard-coded TTL literals in the cache/Redis stores.
    /// </summary>
    public class IdempotencyOptions
    {
        /// <summary>
        /// How long an in-flight (created but not yet completed) record is considered
        /// actively processing. After this window an in-progress EF record is recovered
        /// to a retryable state; cache/Redis in-flight markers expire after it. Default 15 minutes.
        /// </summary>
        public TimeSpan InFlightTtl { get; set; } = TimeSpan.FromMinutes(15);

        /// <summary>
        /// How long a completed record (and its stored response) is retained and replayable.
        /// Drives the EF <c>ExpiresAt</c> on completion and the cache/Redis completed TTL. Default 24 hours.
        /// </summary>
        public TimeSpan CompletedRetention { get; set; } = TimeSpan.FromHours(24);

        /// <summary>
        /// Maximum accepted length of a caller-supplied idempotency key. Default 128.
        /// </summary>
        public int MaxKeyLength { get; set; } = 128;

        /// <summary>
        /// How often the EF purge/recovery hosted service runs. Default 5 minutes.
        /// </summary>
        public TimeSpan PurgeInterval { get; set; } = TimeSpan.FromMinutes(5);
    }
}
