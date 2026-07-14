using System.Collections.Concurrent;

namespace Juice.Messaging.Idempotency
{
    internal class InMemoryIdempotencyService : IIdempotencyService
    {
        private readonly ConcurrentDictionary<string, RequestState> _requests = new();
        private readonly ConcurrentDictionary<string, object?> _results = new();
        private readonly ConcurrentDictionary<string, string?> _hashes = new();
        private readonly IMessageSerializer? _serializer;

        public InMemoryIdempotencyService(IMessageSerializer? serializer = null)
        {
            _serializer = serializer;
        }

        public ValueTask TryCompleteRequestAsync(string scope, string key, bool success, object? result = null, CancellationToken cancellationToken = default)
        {
            var requestKey = GetKey(scope, key);

            if (success)
            {
                // Mark as completed and cache result
                _requests[requestKey] = RequestState.Completed;
                if (result != null)
                {
                    _results[requestKey] = result;
                }
            }
            else
            {
                // Remove failed request to allow retry
                _requests.TryRemove(requestKey, out _);
                _results.TryRemove(requestKey, out _);
                _hashes.TryRemove(requestKey, out _);
            }

            return ValueTask.CompletedTask;
        }

        public ValueTask<IdempotencyResult> TryBeginRequestAsync(string scope, string key,
            string? requestHash = null, CancellationToken cancellationToken = default)
        {
            var requestKey = GetKey(scope, key);

            // Atomic add is the concurrency guard: exactly one caller creates the marker.
            if (_requests.TryAdd(requestKey, RequestState.InProgress))
            {
                _hashes[requestKey] = requestHash;
                return ValueTask.FromResult(new IdempotencyResult(IdempotencyOutcome.Created));
            }

            // Same key, materially different payload → conflict (FR-005).
            if (_hashes.TryGetValue(requestKey, out var storedHash)
                && !string.IsNullOrEmpty(storedHash) && !string.IsNullOrEmpty(requestHash)
                && !string.Equals(storedHash, requestHash, StringComparison.Ordinal))
            {
                return ValueTask.FromResult(new IdempotencyResult(IdempotencyOutcome.Conflict));
            }

            if (_requests.TryGetValue(requestKey, out var state) && state == RequestState.Completed)
            {
                string? stored = null;
                if (_results.TryGetValue(requestKey, out var cached) && cached != null)
                {
                    // Always serialize so StoredResult round-trips through the consumer's Deserialize<T>,
                    // matching the EF/DistributedCache/Redis stores. Returning a raw string here would emit
                    // unquoted, non-JSON text that breaks deserialization for string-typed results.
                    stored = _serializer?.Serialize(cached);
                }
                return ValueTask.FromResult(new IdempotencyResult(IdempotencyOutcome.Completed, stored));
            }

            return ValueTask.FromResult(new IdempotencyResult(IdempotencyOutcome.InProgress));
        }

        private static string GetKey(string scope, string key) => $"{scope}:{key}";

        private enum RequestState
        {
            InProgress,
            Completed
        }
    }
}
