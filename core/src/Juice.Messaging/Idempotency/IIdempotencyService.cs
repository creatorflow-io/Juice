namespace Juice.Messaging.Idempotency
{
    public interface IIdempotencyService
    {
        ValueTask TryCompleteRequestAsync(string scope, string key, bool success,
            object? result = default, CancellationToken cancellationToken = default);

        /// <summary>
        /// Begin (or resume) a request in a single round-trip, returning a rich outcome that the
        /// HTTP layer maps to a status. Applies an atomic <c>(scope, key)</c> concurrency guard and
        /// distinguishes Created / InProgress / Completed / Conflict and, when a fingerprint is
        /// supplied, detects reuse of a key with a materially different payload. Pair each
        /// <see cref="IdempotencyOutcome.Created"/> with a <see cref="TryCompleteRequestAsync"/> call.
        /// </summary>
        /// <param name="scope">Scope (endpoint id + tenant), already composed by the caller.</param>
        /// <param name="key">Caller-supplied idempotency key.</param>
        /// <param name="requestHash">
        /// Optional request fingerprint. When a record already exists with a different hash the
        /// result is <see cref="IdempotencyOutcome.Conflict"/>. Stores that do not persist a
        /// fingerprint ignore this argument.
        /// </param>
        ValueTask<IdempotencyResult> TryBeginRequestAsync(string scope, string key,
            string? requestHash = null, CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Distinct outcomes of <see cref="IIdempotencyService.TryBeginRequestAsync"/>.
    /// </summary>
    public enum IdempotencyOutcome
    {
        /// <summary>A fresh record was created; the caller should execute the operation.</summary>
        Created = 0,

        /// <summary>Another execution holds the key and is still in flight; the caller should signal 409.</summary>
        InProgress = 1,

        /// <summary>The key already completed; replay <see cref="IdempotencyResult.StoredResult"/>.</summary>
        Completed = 2,

        /// <summary>The key was reused with a materially different payload; the caller should signal 422.</summary>
        Conflict = 3
    }

    /// <summary>
    /// Outcome of <see cref="IIdempotencyService.TryBeginRequestAsync"/>. <see cref="StoredResult"/>
    /// is the raw serialized result captured on completion, replayed on <see cref="IdempotencyOutcome.Completed"/>.
    /// </summary>
    public sealed record IdempotencyResult(IdempotencyOutcome Outcome, string? StoredResult = null);
}
