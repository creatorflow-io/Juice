namespace Juice.EventBus.NATS.Policies
{
    /// ======================= JETSTREAM RETRY =======================
    ///
    ///   {destination}.{event}  ──► durable consumer ──► handler fails
    ///                                   ▲                    │
    ///                                   │  NAK(delay of tier n)
    ///                                   └────────────────────┘
    ///                     (server redelivers, NumDelivered = n + 1)
    ///
    ///   attempts > tiers  ──► publish copy to parking.{destination}.{event} ──► ACK
    ///                         (separate {STREAM}_PARKING stream, no consumer, manual ops)
    ///
    internal record JetStreamRetryPolicy
    {
        /// <summary>
        /// NAK delay for this attempt; null when the maximum is reached.
        /// </summary>
        public TimeSpan? Delay { get; init; }
        public bool IsMaxRetryReached { get; init; }
        public bool IsParkingEnabled { get; init; }
        /// <summary>
        /// Parking subject prefix (<c>DeadLetterDest</c>); null to use the consumer default.
        /// </summary>
        public string? ParkingPrefix { get; init; }
        /// <summary>
        /// The last tier delay, used to retry a failed parking publish.
        /// </summary>
        public TimeSpan LastDelay { get; init; }
    }
}
