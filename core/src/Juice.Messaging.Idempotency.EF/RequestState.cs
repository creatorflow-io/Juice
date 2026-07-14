namespace Juice.Messaging.Idempotency.EF
{
    public enum RequestState
    {
        New = 0,
        Processed = 1,
        Failed = 2,
        /// <summary>
        /// Explicit in-progress marker carrying <see cref="IdempotencyRecord.LockedAt"/>.
        /// Recovered to <see cref="Failed"/> (retryable) once it exceeds the configured in-flight TTL.
        /// Records created through <c>TryBeginRequestAsync</c> are created in this state; <see cref="New"/>
        /// is retained as a legacy value and is treated identically to <see cref="InProgress"/> on reads.
        /// </summary>
        InProgress = 3
    }
}
