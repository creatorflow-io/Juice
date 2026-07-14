using Juice.Extensions.Redis;
using Juice.Messaging.Idempotency;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace Juice.Messaging.Idempotency.Redis
{
    public class RedisIdempotencyService : IIdempotencyService
    {
        /// <summary>
        /// Gets the connection.
        /// </summary>
        /// <value>The connection.</value>
        protected IRedisConnectionProvider ConnectionProvider { get; set; }

        private readonly IMessageSerializer _serializer;
        private readonly ILogger _logger;
        private readonly IdempotencyOptions _options;

        // Redis key prefixes for better organization
        private const string REQUEST_PREFIX = "request";
        private const string RESULT_PREFIX = "result";
        private const string STATE_PREFIX = "state";
        private const string HASH_PREFIX = "hash";

        public RedisIdempotencyService(ILogger<RedisIdempotencyService> logger,
            IRedisConnectionProvider<RedisIdempotencyService> redisConnectionProvider,
            IMessageSerializer serializer,
            IOptions<IdempotencyOptions>? options = null)
        {
            _logger = logger;
            ConnectionProvider = redisConnectionProvider;
            _serializer = serializer;
            _options = options?.Value ?? new IdempotencyOptions();
        }

        /// <summary>
        /// Generates the Redis key for request tracking
        /// Format: "request:{scope}:{key}"
        /// </summary>
        private string GetRequestKey(string scope, string key)
        {
            return $"{REQUEST_PREFIX}:{scope}:{key}";
        }

        /// <summary>
        /// Generates the Redis key for result caching
        /// Format: "result:{scope}:{key}"
        /// </summary>
        private string GetResultKey(string scope, string key)
        {
            return $"{RESULT_PREFIX}:{scope}:{key}";
        }

        /// <summary>
        /// Generates the Redis key for state tracking
        /// Format: "state:{scope}:{key}"
        /// </summary>
        private string GetStateKey(string scope, string key)
        {
            return $"{STATE_PREFIX}:{scope}:{key}";
        }

        /// <summary>
        /// Generates the Redis key for the request fingerprint
        /// Format: "hash:{scope}:{key}"
        /// </summary>
        private string GetHashKey(string scope, string key)
        {
            return $"{HASH_PREFIX}:{scope}:{key}";
        }

        public async ValueTask TryCompleteRequestAsync(string scope, string key, bool success, object? result, CancellationToken cancellationToken)
        {
            var requestKey = GetRequestKey(scope, key);
            var resultKey = GetResultKey(scope, key);
            var stateKey = GetStateKey(scope, key);
            var hashKey = GetHashKey(scope, key);
            var connection = await ConnectionProvider.GetConnectionAsync();
            var db = connection.GetDatabase();

            try
            {
                if (success)
                {
                    // Use Redis transaction to ensure atomicity
                    var transaction = db.CreateTransaction();

                    // Update request timestamp
                    var requestUpdateTask = transaction.StringSetAsync(
                        requestKey,
                        DateTimeOffset.UtcNow.ToString("O"),
                        _options.CompletedRetention,
                        When.Exists);

                    // Extend the fingerprint TTL to the completed-retention window so conflict detection
                    // keeps working for replayed requests (parity with the EF store). No-op if absent.
                    var hashExpireTask = transaction.KeyExpireAsync(hashKey, _options.CompletedRetention);

                    // Store result if provided
                    Task<bool> resultStoreTask = Task.FromResult(true);
                    if (result != null)
                    {
                        var serializedResult = _serializer.Serialize(result);
                        if (!string.IsNullOrEmpty(serializedResult))
                        {
                            resultStoreTask = transaction.StringSetAsync(
                                resultKey,
                                serializedResult,
                                _options.CompletedRetention);
                        }
                    }

                    // Mark as completed
                    var stateUpdateTask = transaction.StringSetAsync(
                        stateKey,
                        "completed",
                        _options.CompletedRetention);

                    // Execute transaction
                    var executed = await transaction.ExecuteAsync();

                    if (executed)
                    {
                        await Task.WhenAll(requestUpdateTask, resultStoreTask, stateUpdateTask, hashExpireTask);

                        _logger.LogDebug(
                            "Successfully completed request {RequestId} for {CommandType}. Result cached: {HasResult}",
                            key, scope, result != null);
                    }
                    else
                    {
                        _logger.LogWarning(
                            "Failed to execute transaction for request {RequestId} for {CommandType}",
                            key, scope);
                    }
                }
                else
                {
                    // Failed request - delete all related keys
                    string lua_script = @"
                        local requestKey = KEYS[1]
                        local resultKey = KEYS[2]
                        local stateKey = KEYS[3]
                        local hashKey = KEYS[4]
                        local expectedValue = ARGV[1]

                        if redis.call('EXISTS', requestKey) == 1 then
                            redis.call('DEL', requestKey)
                            redis.call('DEL', resultKey)
                            redis.call('DEL', stateKey)
                            redis.call('DEL', hashKey)
                            return 1
                        else
                            return 0
                        end
                    ";

                    var res = await db.ScriptEvaluateAsync(
                        lua_script,
                        new RedisKey[] { requestKey, resultKey, stateKey, hashKey },
                        new RedisValue[] { "" });

                    var deleted = (long)res;
                    if (deleted == 0)
                    {
                        _logger.LogWarning(
                            "Request {RequestId} for {CommandType} not found or already deleted",
                            key, scope);
                    }
                    else
                    {
                        _logger.LogDebug(
                            "Deleted failed request {RequestId} for {CommandType}",
                            key, scope);
                    }
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
            var hashKey = GetHashKey(scope, key);

            IConnectionMultiplexer connection;
            try
            {
                connection = await ConnectionProvider.GetConnectionAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting Redis connection to begin request {RequestId} for {CommandType}", key, scope);
                return new IdempotencyResult(IdempotencyOutcome.InProgress);
            }

            try
            {
                var db = connection.GetDatabase();

                // SET NX is the atomic concurrency guard: exactly one caller creates the marker.
                var created = await db.StringSetAsync(requestKey, DateTimeOffset.UtcNow.ToString("O"),
                    _options.InFlightTtl, When.NotExists);
                if (created)
                {
                    await db.StringSetAsync(stateKey, "pending", _options.InFlightTtl, When.NotExists);
                    if (!string.IsNullOrEmpty(requestHash))
                    {
                        await db.StringSetAsync(hashKey, requestHash, _options.InFlightTtl, When.NotExists);
                    }
                    return new IdempotencyResult(IdempotencyOutcome.Created);
                }

                // Same key, materially different payload → conflict (FR-005).
                var storedHash = await db.StringGetAsync(hashKey);
                if (!storedHash.IsNullOrEmpty && !string.IsNullOrEmpty(requestHash)
                    && !string.Equals(storedHash.ToString(), requestHash, StringComparison.Ordinal))
                {
                    return new IdempotencyResult(IdempotencyOutcome.Conflict);
                }

                var state = await db.StringGetAsync(stateKey);
                if (state == "completed")
                {
                    var stored = await db.StringGetAsync(resultKey);
                    return new IdempotencyResult(IdempotencyOutcome.Completed,
                        stored.IsNullOrEmpty ? null : stored.ToString());
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
