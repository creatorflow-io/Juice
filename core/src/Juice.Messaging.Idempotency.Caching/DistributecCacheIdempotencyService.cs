using Juice.Messaging.Idempotency;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Juice.Messaging.Idempotency.Cache
{
    internal class DistributecCacheIdempotencyService : IIdempotencyService
    {
        private readonly IDistributedCache _cache;
        private readonly IMessageSerializer _serializer;
        private readonly ILogger _logger;
        private readonly IdempotencyOptions _options;

        // Cache key prefixes for better organization
        private const string REQUEST_PREFIX = "idempotency:request";
        private const string RESULT_PREFIX = "idempotency:result";
        private const string STATE_PREFIX = "idempotency:state";
        private const string HASH_PREFIX = "idempotency:hash";

        public DistributecCacheIdempotencyService(
            IDistributedCache cache,
            IMessageSerializer serializer,
            ILogger<DistributecCacheIdempotencyService> logger,
            IOptions<IdempotencyOptions>? options = null)
        {
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));
            _serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _options = options?.Value ?? new IdempotencyOptions();
        }

        /// <summary>
        /// Generates the cache key for request tracking
        /// Format: "idempotency:request:{scope}:{key}"
        /// </summary>
        private string GetRequestKey(string scope, string key)
        {
            return $"{REQUEST_PREFIX}:{scope}:{key}";
        }

        /// <summary>
        /// Generates the cache key for result caching
        /// Format: "idempotency:result:{scope}:{key}"
        /// </summary>
        private string GetResultKey(string scope, string key)
        {
            return $"{RESULT_PREFIX}:{scope}:{key}";
        }

        /// <summary>
        /// Generates the cache key for state tracking
        /// Format: "idempotency:state:{scope}:{key}"
        /// </summary>
        private string GetStateKey(string scope, string key)
        {
            return $"{STATE_PREFIX}:{scope}:{key}";
        }

        /// <summary>
        /// Generates the cache key for the request fingerprint
        /// Format: "idempotency:hash:{scope}:{key}"
        /// </summary>
        private string GetHashKey(string scope, string key)
        {
            return $"{HASH_PREFIX}:{scope}:{key}";
        }

        public async ValueTask TryCompleteRequestAsync(string scope, string key, bool success, object? result = null, CancellationToken cancellationToken = default)
        {
            var requestKey = GetRequestKey(scope, key);
            var resultKey = GetResultKey(scope, key);
            var stateKey = GetStateKey(scope, key);
            var hashKey = GetHashKey(scope, key);

            try
            {
                if (success)
                {
                    var cacheOptions = new DistributedCacheEntryOptions
                    {
                        AbsoluteExpirationRelativeToNow = _options.CompletedRetention
                    };

                    // Update request timestamp
                    await _cache.SetStringAsync(
                        requestKey,
                        DateTimeOffset.UtcNow.ToString("O"),
                        cacheOptions,
                        cancellationToken);

                    // Extend the fingerprint TTL to the completed-retention window so conflict detection
                    // keeps working for replayed requests (parity with the EF store).
                    var existingHash = await _cache.GetStringAsync(hashKey, cancellationToken);
                    if (!string.IsNullOrEmpty(existingHash))
                    {
                        await _cache.SetStringAsync(hashKey, existingHash, cacheOptions, cancellationToken);
                    }

                    // Store result if provided
                    if (result != null)
                    {
                        var serializedResult = _serializer.Serialize(result);
                        if (!string.IsNullOrEmpty(serializedResult))
                        {
                            await _cache.SetStringAsync(
                                resultKey,
                                serializedResult,
                                cacheOptions,
                                cancellationToken);
                        }
                    }

                    // Mark as completed
                    await _cache.SetStringAsync(
                        stateKey,
                        "completed",
                        cacheOptions,
                        cancellationToken);

                    _logger.LogDebug(
                        "Successfully completed request {RequestId} for {CommandType}. Result cached: {HasResult}",
                        key, scope, result != null);
                }
                else
                {
                    // Failed request - remove all related keys to allow retry
                    await _cache.RemoveAsync(requestKey, cancellationToken);
                    await _cache.RemoveAsync(resultKey, cancellationToken);
                    await _cache.RemoveAsync(stateKey, cancellationToken);
                    await _cache.RemoveAsync(hashKey, cancellationToken);

                    _logger.LogDebug(
                        "Removed failed request {RequestId} for {CommandType}",
                        key, scope);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error while trying to complete request {RequestId} for {CommandType}. Success: {Success}",
                    key, scope, success);
            }
        }

        public async ValueTask<IdempotencyResult> TryBeginRequestAsync(string scope, string key,
            string? requestHash = null, CancellationToken cancellationToken = default)
        {
            var requestKey = GetRequestKey(scope, key);
            var stateKey = GetStateKey(scope, key);
            var resultKey = GetResultKey(scope, key);

            try
            {
                var hashKey = GetHashKey(scope, key);

                var existingRequest = await _cache.GetStringAsync(requestKey, cancellationToken);
                if (existingRequest == null)
                {
                    var cacheOptions = new DistributedCacheEntryOptions
                    {
                        AbsoluteExpirationRelativeToNow = _options.InFlightTtl
                    };
                    await _cache.SetStringAsync(requestKey, DateTimeOffset.UtcNow.ToString("O"), cacheOptions, cancellationToken);
                    await _cache.SetStringAsync(stateKey, "pending", cacheOptions, cancellationToken);
                    if (!string.IsNullOrEmpty(requestHash))
                    {
                        await _cache.SetStringAsync(hashKey, requestHash, cacheOptions, cancellationToken);
                    }
                    return new IdempotencyResult(IdempotencyOutcome.Created);
                }

                // Same key, materially different payload → conflict (FR-005).
                var storedHash = await _cache.GetStringAsync(hashKey, cancellationToken);
                if (!string.IsNullOrEmpty(storedHash) && !string.IsNullOrEmpty(requestHash)
                    && !string.Equals(storedHash, requestHash, StringComparison.Ordinal))
                {
                    return new IdempotencyResult(IdempotencyOutcome.Conflict);
                }

                var state = await _cache.GetStringAsync(stateKey, cancellationToken);
                if (state == "completed")
                {
                    var stored = await _cache.GetStringAsync(resultKey, cancellationToken);
                    return new IdempotencyResult(IdempotencyOutcome.Completed, stored);
                }

                return new IdempotencyResult(IdempotencyOutcome.InProgress);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error beginning request {RequestId} for {CommandType}", key, scope);
                return new IdempotencyResult(IdempotencyOutcome.InProgress);
            }
        }
    }
}
