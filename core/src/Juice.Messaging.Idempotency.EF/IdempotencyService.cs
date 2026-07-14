using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Juice.Messaging.Idempotency.EF
{
    internal class IdempotencyService: IIdempotencyService
    {
        private IdempotencyContext _context;
        private readonly ILogger _logger;
        private readonly IMessageSerializer _serializer;
        private readonly INodeIdentity? _nodeIdentity;
        private readonly IdempotencyOptions _options;

        public IdempotencyService(IdempotencyContext context,
            ILogger<IdempotencyService> logger,
            IMessageSerializer serializer,
            INodeIdentity? nodeIdentity = null,
            IOptions<IdempotencyOptions>? options = null)
        {
            _context = context;
            _logger = logger;
            _serializer = serializer;
            _nodeIdentity = nodeIdentity;
            _options = options?.Value ?? new IdempotencyOptions();
        }

        public async ValueTask TryCompleteRequestAsync(string scope, string key, bool success, object? result, CancellationToken cancellationToken)
        {
            try
            {
                var res = _serializer.Serialize(result);
                var nodeId = _nodeIdentity?.NodeId;
                var now = DateTimeOffset.Now;
                // On success the record is retained (and replayable) for CompletedRetention; on failure the
                // in-flight expiry set at begin is preserved so the key stays retryable then purges.
                DateTimeOffset? completedExpiry = now + _options.CompletedRetention;
                await
                    _context.IdempotencyRecords.Where(r => r.Scope == scope && r.Key == key)
                    .ExecuteUpdateAsync(r =>
                        r.SetProperty(r => r.State,
                        success ? RequestState.Processed : RequestState.Failed)
                        .SetProperty(r => r.CompletedAt, now)
                        .SetProperty(r => r.Result, res)
                        .SetProperty(r => r.ProcessedBy, nodeId)
                        .SetProperty(r => r.LockedAt, (DateTimeOffset?)null)
                        .SetProperty(r => r.ExpiresAt, r => success ? completedExpiry : r.ExpiresAt)
                        , cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error completing request {RequestId} for scope {CommandName}", key, scope);
            }
        }

        public async ValueTask<IdempotencyResult> TryBeginRequestAsync(string scope, string key,
            string? requestHash = null, CancellationToken cancellationToken = default)
        {
            var now = DateTimeOffset.Now;
            var staleBefore = now - _options.InFlightTtl;

            var record = await _context.IdempotencyRecords.AsNoTracking()
                .FirstOrDefaultAsync(r => r.Scope == scope && r.Key == key, cancellationToken);

            if (record == null)
            {
                try
                {
                    var newRecord = new IdempotencyRecord(scope, key);
                    newRecord.SetProcessedBy(_nodeIdentity?.NodeId);
                    newRecord.BeginProcessing(requestHash, now, _options.InFlightTtl);
                    _context.IdempotencyRecords.Add(newRecord);
                    await _context.SaveChangesAsync(cancellationToken);
                    return new IdempotencyResult(IdempotencyOutcome.Created);
                }
                catch (DbUpdateException)
                {
                    // Lost the create race — fall through to inspect the winner's record.
                    record = await _context.IdempotencyRecords.AsNoTracking()
                        .FirstOrDefaultAsync(r => r.Scope == scope && r.Key == key, cancellationToken);
                    if (record == null)
                    {
                        return new IdempotencyResult(IdempotencyOutcome.InProgress);
                    }
                }
            }

            var expired = record.ExpiresAt.HasValue && record.ExpiresAt.Value < now;
            var stale = (record.State == RequestState.New || record.State == RequestState.InProgress)
                        && record.LockedAt.HasValue && record.LockedAt.Value < staleBefore;

            // Retryable: prior failure, expired retention, or a crashed in-flight lock. Reclaim atomically —
            // the WHERE predicate guarantees exactly one concurrent caller wins and re-begins the record.
            if (record.State == RequestState.Failed || expired || stale)
            {
                var nodeId = _nodeIdentity?.NodeId;
                DateTimeOffset? inFlightExpiry = now + _options.InFlightTtl;
                var reclaimed = await _context.IdempotencyRecords
                    .Where(r => r.Scope == scope && r.Key == key
                        && (r.State == RequestState.Failed
                            || (r.ExpiresAt != null && r.ExpiresAt < now)
                            || ((r.State == RequestState.New || r.State == RequestState.InProgress)
                                && r.LockedAt != null && r.LockedAt < staleBefore)))
                    .ExecuteUpdateAsync(s => s
                        .SetProperty(r => r.State, RequestState.InProgress)
                        .SetProperty(r => r.LockedAt, now)
                        .SetProperty(r => r.ExpiresAt, inFlightExpiry)
                        .SetProperty(r => r.RequestHash, requestHash)
                        .SetProperty(r => r.Result, (string?)null)
                        .SetProperty(r => r.CompletedAt, (DateTimeOffset?)null)
                        .SetProperty(r => r.ProcessedBy, nodeId)
                        , cancellationToken);

                if (reclaimed == 1)
                {
                    return new IdempotencyResult(IdempotencyOutcome.Created);
                }

                // Another caller reclaimed first — re-read and report its current state.
                record = await _context.IdempotencyRecords.AsNoTracking()
                    .FirstOrDefaultAsync(r => r.Scope == scope && r.Key == key, cancellationToken);
                if (record == null)
                {
                    return new IdempotencyResult(IdempotencyOutcome.InProgress);
                }
            }

            // Fingerprint conflict: same key, materially different payload.
            if (!string.IsNullOrEmpty(record.RequestHash) && !string.IsNullOrEmpty(requestHash)
                && !string.Equals(record.RequestHash, requestHash, StringComparison.Ordinal))
            {
                return new IdempotencyResult(IdempotencyOutcome.Conflict);
            }

            if (record.State == RequestState.Processed)
            {
                return new IdempotencyResult(IdempotencyOutcome.Completed, record.Result);
            }

            // New / InProgress and still within the in-flight window.
            return new IdempotencyResult(IdempotencyOutcome.InProgress);
        }

    }

}
