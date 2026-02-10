using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;

namespace Juice.Messaging.Idempotency.Cache
{
    internal class DistributecCacheIdempotencyService : IIdempotencyService
    {
        private readonly IDistributedCache _cache;
        private readonly IMessageSerializer _serializer;
        private readonly ILogger _logger;

        // Cache key prefixes for better organization
        private const string REQUEST_PREFIX = "idempotency:request";
        private const string RESULT_PREFIX = "idempotency:result";
        private const string STATE_PREFIX = "idempotency:state";

        public DistributecCacheIdempotencyService(
            IDistributedCache cache,
            IMessageSerializer serializer,
            ILogger<DistributecCacheIdempotencyService> logger)
        {
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));
            _serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
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

        public async ValueTask TryCompleteRequestAsync(string scope, string key, bool success, object? result = null, CancellationToken cancellationToken = default)
        {
            var requestKey = GetRequestKey(scope, key);
            var resultKey = GetResultKey(scope, key);
            var stateKey = GetStateKey(scope, key);

            try
            {
                if (success)
                {
                    var cacheOptions = new DistributedCacheEntryOptions
                    {
                        AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(24)
                    };

                    // Update request timestamp
                    await _cache.SetStringAsync(
                        requestKey,
                        DateTimeOffset.UtcNow.ToString("O"),
                        cacheOptions,
                        cancellationToken);

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

        public async ValueTask<IOperationResult> TryCreateRequestAsync(string scope, string key, CancellationToken cancellationToken = default)
        {
            var requestKey = GetRequestKey(scope, key);
            var stateKey = GetStateKey(scope, key);

            try
            {
                // Check if request already exists
                var existingRequest = await _cache.GetStringAsync(requestKey, cancellationToken);
                if (existingRequest != null)
                {
                    _logger.LogWarning(
                        "Request marker for {RequestId} for {CommandType} already exists",
                        key, scope);
                    return OperationResult.Failed(
                        $"Request marker for '{key}' in scope '{scope}' already exists.");
                }

                var cacheOptions = new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(15)
                };

                // Create request marker
                await _cache.SetStringAsync(
                    requestKey,
                    DateTimeOffset.UtcNow.ToString("O"),
                    cacheOptions,
                    cancellationToken);

                // Create state marker
                await _cache.SetStringAsync(
                    stateKey,
                    "pending",
                    cacheOptions,
                    cancellationToken);

                _logger.LogDebug(
                    "Successfully created request marker for {RequestId} for {CommandType}",
                    key, scope);

                return OperationResult.Success;
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error while trying to create request {RequestId} for {CommandType}",
                    key, scope);
                return OperationResult.Failed(
                    $"Error creating request marker for '{key}' in scope '{scope}'.");
            }
        }

        public async ValueTask<IOperationResult<TResult>> TryCreateRequestAsync<TResult>(string scope, string key, CancellationToken cancellationToken = default)
        {
            var requestKey = GetRequestKey(scope, key);
            var stateKey = GetStateKey(scope, key);

            try
            {
                // Check if request already exists
                var existingRequest = await _cache.GetStringAsync(requestKey, cancellationToken);
                if (existingRequest != null)
                {
                    _logger.LogWarning(
                        "Request marker for {RequestId} for {CommandType} already exists",
                        key, scope);

                    // Try to get cached result
                    var cachedResult = await GetCachedResultAsync<TResult>(scope, key, cancellationToken);
                    return OperationResult.Failed(
                        $"Request marker for '{key}' in scope '{scope}' already exists.",
                        cachedResult);
                }

                var cacheOptions = new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(15)
                };

                // Create request marker
                await _cache.SetStringAsync(
                    requestKey,
                    DateTimeOffset.UtcNow.ToString("O"),
                    cacheOptions,
                    cancellationToken);

                // Create state marker
                await _cache.SetStringAsync(
                    stateKey,
                    "pending",
                    cacheOptions,
                    cancellationToken);

                _logger.LogDebug(
                    "Successfully created request marker for {RequestId} for {CommandType}",
                    key, scope);

                return OperationResult.Result<TResult>(default);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error while trying to create request {RequestId} for {CommandType}",
                    key, scope);

                var cachedResult = await GetCachedResultAsync<TResult>(scope, key, cancellationToken);
                return OperationResult.Failed(
                    $"Error creating request marker for '{key}' in scope '{scope}'.",
                    cachedResult);
            }
        }

        private async ValueTask<TResult?> GetCachedResultAsync<TResult>(string scope, string key, CancellationToken cancellationToken)
        {
            try
            {
                var resultKey = GetResultKey(scope, key);
                var cachedValue = await _cache.GetStringAsync(resultKey, cancellationToken);

                if (string.IsNullOrEmpty(cachedValue))
                {
                    _logger.LogDebug(
                        "Cached result is empty for {Scope} {RequestId}",
                        scope, key);
                    return default;
                }

                var result = _serializer.Deserialize<TResult>(cachedValue);

                _logger.LogDebug(
                    "Retrieved cached result for {Scope} {RequestId}. Type: {ResultType}",
                    scope, key, typeof(TResult).Name);

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error while retrieving cached result for {Scope} {RequestId}. {Message}",
                    scope, key, ex.Message);
                return default;
            }
        }
    }
}
