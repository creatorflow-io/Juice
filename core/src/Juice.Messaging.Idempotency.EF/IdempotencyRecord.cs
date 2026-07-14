namespace Juice.Messaging.Idempotency.EF
{
    public class IdempotencyRecord
    {
        /// <summary>
        /// For EFCore binding
        /// </summary>
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
        private IdempotencyRecord() { }
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.

        public IdempotencyRecord(string scope, string key)
        {
            Scope = scope;
            Key = key;
            CreatedAt = DateTimeOffset.Now;
            State = RequestState.New;
        }
        public string Key { get; private set; }
        public string Scope { get; set; }
        public DateTimeOffset CreatedAt { get; private set; }
        public RequestState State { get; private set; }
        public DateTimeOffset? CompletedAt { get; private set; }
        public string? Result { get; private set; }
        public string? ProcessedBy { get; private set; }

        /// <summary>
        /// Fingerprint/hash of the original request payload, used to detect reuse of a key
        /// with a materially different payload (conflict). NEW.
        /// </summary>
        public string? RequestHash { get; private set; }

        /// <summary>
        /// Retention boundary. When in the past the record is eligible for purge (EF) and a request
        /// reusing the key is treated as new. Set to <c>now + InFlightTtl</c> on create and
        /// <c>now + CompletedRetention</c> on successful completion. NEW.
        /// </summary>
        public DateTimeOffset? ExpiresAt { get; private set; }

        /// <summary>
        /// When processing began (the record was created / locked). Drives in-progress timeout recovery. NEW.
        /// </summary>
        public DateTimeOffset? LockedAt { get; private set; }

        internal void SetProcessedBy(string? nodeId)
        {
            ProcessedBy = nodeId;
        }

        /// <summary>
        /// Marks the record as actively in-progress (created / locked): sets <see cref="State"/> to
        /// <see cref="RequestState.InProgress"/>, stamps <see cref="LockedAt"/> and the in-flight
        /// <see cref="ExpiresAt"/>, and records the request fingerprint.
        /// </summary>
        internal void BeginProcessing(string? requestHash, DateTimeOffset now, TimeSpan inFlightTtl)
        {
            State = RequestState.InProgress;
            LockedAt = now;
            ExpiresAt = now + inFlightTtl;
            RequestHash = requestHash;
        }
    }
}
