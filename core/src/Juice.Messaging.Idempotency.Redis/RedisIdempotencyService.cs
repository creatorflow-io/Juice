using Juice.Extensions.Redis;
using Microsoft.Extensions.Logging;
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

        // Redis key prefixes for better organization
        private const string REQUEST_PREFIX = "request";
        private const string RESULT_PREFIX = "result";
        private const string STATE_PREFIX = "state";

        public RedisIdempotencyService(ILogger<RedisIdempotencyService> logger,
            IRedisConnectionProvider<RedisIdempotencyService> redisConnectionProvider,
            IMessageSerializer serializer)
        {
            _logger = logger;
            ConnectionProvider = redisConnectionProvider;
            _serializer = serializer;
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

        public async ValueTask TryCompleteRequestAsync(string scope, string key, bool success, object? result, CancellationToken cancellationToken)
        {
            var requestKey = GetRequestKey(scope, key);
            var resultKey = GetResultKey(scope, key);
            var stateKey = GetStateKey(scope, key);
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
                        TimeSpan.FromHours(24),
                        When.Exists);

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
                                TimeSpan.FromHours(24));
                        }
                    }

                    // Mark as completed
                    var stateUpdateTask = transaction.StringSetAsync(
                        stateKey,
                        "completed",
                        TimeSpan.FromHours(24));

                    // Execute transaction
                    var executed = await transaction.ExecuteAsync();

                    if (executed)
                    {
                        await Task.WhenAll(requestUpdateTask, resultStoreTask, stateUpdateTask);

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
                        local expectedValue = ARGV[1]
                        
                        if redis.call('EXISTS', requestKey) == 1 then
                            redis.call('DEL', requestKey)
                            redis.call('DEL', resultKey)
                            redis.call('DEL', stateKey)
                            return 1
                        else
                            return 0
                        end
                    ";

                    var res = await db.ScriptEvaluateAsync(
                        lua_script,
                        new RedisKey[] { requestKey, resultKey, stateKey },
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

        public async ValueTask<IOperationResult<T>> TryCreateRequestAsync<T>(string scope, string key, CancellationToken cancellationToken)
        {
            var requestKey = GetRequestKey(scope, key);
            var resultKey = GetResultKey(scope, key);
            var stateKey = GetStateKey(scope, key);

            IConnectionMultiplexer connection;
            try
            {
                connection = await ConnectionProvider.GetConnectionAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error while trying to get Redis connection for request {RequestId} for {CommandType}",
                    key, scope);
                return OperationResult.Failed<T>(ex, 
                    $"Error while trying to get Redis connection.");
            }

            try
            {
                var db = connection.GetDatabase();

                // Use transaction to ensure both keys are created together
                var transaction = db.CreateTransaction();

                var requestCreateTask = transaction.StringSetAsync(
                    requestKey,
                    "",
                    TimeSpan.FromMinutes(15),
                    When.NotExists);

                var stateCreateTask = transaction.StringSetAsync(
                    stateKey,
                    "pending",
                    TimeSpan.FromMinutes(15),
                    When.NotExists);

                var executed = await transaction.ExecuteAsync();

                if (!executed)
                {
                    _logger.LogWarning(
                        "Request marker for {RequestId} for {CommandType} already exists (transaction failed)",
                        key, scope);
                    var result = await GetCachedResultAsync<T>(scope, key);
                    return OperationResult.Failed(
                        $"Request marker for '{key}' in scope '{scope}' already exists.",
                        result);
                }

                var created = await Task.WhenAll(requestCreateTask, stateCreateTask);
                if (!created.All(c => c))
                {
                    _logger.LogWarning(
                        "Request marker for {RequestId} for {CommandType} already exists",
                        key, scope);
                    var result = await GetCachedResultAsync<T>(scope, key);
                    return OperationResult.Failed(
                        $"Request marker for '{key}' in scope '{scope}' already exists.",
                        result);
                }

                _logger.LogDebug(
                    "Successfully created request marker for {RequestId} for {CommandType}",
                    key, scope);
                return OperationResult.Result<T>(default);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error while trying to create request {RequestId} for {CommandType}",
                    key, scope);
                var result = await GetCachedResultAsync<T>(scope, key);
                return OperationResult.Failed(
                    $"Request marker for '{key}' in scope '{scope}' already exists.",
                    result);
            }
        }

        private async ValueTask<TR?> GetCachedResultAsync<TR>(string scope, string key)
        {
            var connection = await ConnectionProvider.GetConnectionAsync();
            var db = connection.GetDatabase();

            try
            {
                var resultKey = GetResultKey(scope, key);
                var cachedValue = await db.StringGetAsync(resultKey);

                if (cachedValue.IsNullOrEmpty)
                {
                    _logger.LogDebug(
                        "Cached result is empty for {Scope} {RequestId}",
                        scope, key);
                    return default;
                }

                var result = _serializer.Deserialize<TR>(cachedValue!, default);

                _logger.LogDebug(
                    "Retrieved cached result for {Scope} {RequestId}. Type: {ResultType}",
                    scope, key, typeof(TR).Name);

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

        public async ValueTask<IOperationResult> TryCreateRequestAsync(string scope, string key, CancellationToken cancellationToken)
        {
            var requestKey = GetRequestKey(scope, key);
            var stateKey = GetStateKey(scope, key);
            var connection = await ConnectionProvider.GetConnectionAsync();
            var db = connection.GetDatabase();

            try
            {
                // Use transaction to ensure both keys are created together
                var transaction = db.CreateTransaction();

                var requestCreateTask = transaction.StringSetAsync(
                    requestKey,
                    "",
                    TimeSpan.FromMinutes(15),
                    When.NotExists);

                var stateCreateTask = transaction.StringSetAsync(
                    stateKey,
                    "pending",
                    TimeSpan.FromMinutes(15),
                    When.NotExists);

                var executed = await transaction.ExecuteAsync();

                if (!executed)
                {
                    _logger.LogWarning(
                        "Request marker for {RequestId} for {CommandType} already exists (transaction failed)",
                        key, scope);
                    return OperationResult.Failed(
                        $"Request marker for '{key}' in scope '{scope}' already exists.");
                }

                var created = await Task.WhenAll(requestCreateTask, stateCreateTask);
                if (!created.All(c => c))
                {
                    _logger.LogWarning(
                        "Request marker for {RequestId} for {CommandType} already exists",
                        key, scope);
                    return OperationResult.Failed(
                        $"Request marker for '{key}' in scope '{scope}' already exists.");
                }

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
                    $"Request marker for '{key}' in scope '{scope}' already exists."
                   );
            }
        }
    }
}
