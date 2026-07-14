namespace Juice.MediatR.Behaviors
{
    /// <summary>
    /// Thrown by <see cref="IdempotencyRequestBehavior{TRequest,TResponse}"/> (and the void variant) when a
    /// request arrives for a <c>(scope, key)</c> whose original execution is still in flight. Callers should
    /// treat this as "retry shortly": for message-bus consumers the exception propagates to a NACK so the
    /// message is redelivered later, by which time the original has completed and the retry replays its result.
    /// </summary>
    public class DuplicateRequestInProgressException : Exception
    {
        public string Scope { get; }
        public string Key { get; }

        public DuplicateRequestInProgressException(string scope, string key)
            : base($"A request for scope '{scope}' with idempotency key '{key}' is already in progress.")
        {
            Scope = scope;
            Key = key;
        }
    }

    /// <summary>
    /// Thrown when an idempotency key is reused with a materially different request payload (fingerprint
    /// mismatch). No effect is applied. Only stores that persist a request fingerprint (EF, InMemory) can
    /// surface this; stores that ignore the fingerprint never raise it.
    /// </summary>
    public class IdempotencyKeyConflictException : Exception
    {
        public string Scope { get; }
        public string Key { get; }

        public IdempotencyKeyConflictException(string scope, string key)
            : base($"Idempotency key '{key}' for scope '{scope}' was already used with a different request payload.")
        {
            Scope = scope;
            Key = key;
        }
    }
}
