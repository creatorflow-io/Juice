using Juice.Extensions.Redis;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace Juice.MediatR.RequestManager.Redis
{
    public class RequestManager : IRequestManager
    {
        /// <summary>  
        /// Gets the connection.  
        /// </summary>  
        /// <value>The connection.</value>  
        protected IRedisConnectionProvider ConnectionProvider { get; set; }

        private readonly IResponseSerializer _serializer;
        private readonly ILogger _logger;

        // Redis key prefixes for better organization
        private const string REQUEST_PREFIX = "request";
        private const string RESULT_PREFIX = "result";
        private const string STATE_PREFIX = "state";

        public RequestManager(ILogger<RequestManager> logger,
            IRedisConnectionProvider<RequestManager> redisConnectionProvider,
            IResponseSerializer serializer)
        {
            _logger = logger;
            ConnectionProvider = redisConnectionProvider;
            _serializer = serializer;
        }

        /// <summary>
        /// Generates the Redis key for request tracking
        /// Format: "request:{TypeName}:{Id}"
        /// </summary>
        private string GetRequestKey<T>(Guid id)
        {
            return $"{REQUEST_PREFIX}:{typeof(T).Name}:{id}";
        }

        /// <summary>
        /// Generates the Redis key for result caching
        /// Format: "result:{TypeName}:{Id}"
        /// </summary>
        private string GetResultKey<T>(Guid id)
        {
            return $"{RESULT_PREFIX}:{typeof(T).Name}:{id}";
        }

        /// <summary>
        /// Generates the Redis key for state tracking
        /// Format: "state:{TypeName}:{Id}"
        /// </summary>
        private string GetStateKey<T>(Guid id)
        {
            return $"{STATE_PREFIX}:{typeof(T).Name}:{id}";
        }

        public async ValueTask TryCompleteRequestAsync<T>(Guid id, bool success, object? result)
            where T : IBaseRequest
        {
            var requestKey = GetRequestKey<T>(id);
            var resultKey = GetResultKey<T>(id);
            var stateKey = GetStateKey<T>(id);
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
                        var serializedResult = _serializer.SerializeResponse(result);
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
                            "Successfully completed request {RequestId} for command {CommandType}. Result cached: {HasResult}",
                            id, typeof(T).Name, result != null);
                    }
                    else
                    {
                        _logger.LogWarning(
                            "Failed to execute transaction for request {RequestId} for command {CommandType}",
                            id, typeof(T).Name);
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
                            "Request {RequestId} for command {CommandType} not found or already deleted",
                            id, typeof(T).Name);
                    }
                    else
                    {
                        _logger.LogDebug(
                            "Deleted failed request {RequestId} for command {CommandType}",
                            id, typeof(T).Name);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error while trying to complete request {RequestId} for command {CommandType}. Success: {Success}",
                    id, typeof(T).Name, success);
            }
        }

        public async ValueTask<bool> TryCreateRequestForCommandAsync<T>(Guid id)
            where T : IBaseRequest
        {
            var requestKey = GetRequestKey<T>(id);
            var stateKey = GetStateKey<T>(id);
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
                        "Request marker for {RequestId} for command {CommandType} already exists (transaction failed)",
                        id, typeof(T).Name);
                    return false;
                }

                var created = await Task.WhenAll(requestCreateTask, stateCreateTask);
                if (!created.All(c => c))
                {
                    _logger.LogWarning(
                        "Request marker for {RequestId} for command {CommandType} already exists",
                        id, typeof(T).Name);
                    return false;
                }

                _logger.LogDebug(
                    "Successfully created request marker for {RequestId} for command {CommandType}",
                    id, typeof(T).Name);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error while trying to create request {RequestId} for command {CommandType}",
                    id, typeof(T).Name);
                return false;
            }
        }

        public async ValueTask<TR?> GetCachedResultAsync<T, TR>(Guid id)
            where T : IBaseRequest
        {
            var connection = await ConnectionProvider.GetConnectionAsync();
            var db = connection.GetDatabase();

            try
            {
                var resultKey = GetResultKey<T>(id);
                var cachedValue = await db.StringGetAsync(resultKey);

                if (cachedValue.IsNullOrEmpty)
                {
                    _logger.LogDebug(
                        "Cached result is empty for request {RequestId}",
                        id);
                    return default;
                }

                var result = _serializer.DeserializeResponse<TR>(cachedValue!);

                _logger.LogDebug(
                    "Retrieved cached result for request {RequestId}. Type: {ResultType}",
                    id, typeof(TR).Name);

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error while retrieving cached result for request {RequestId}. {Message}",
                    id, ex.Message);
                return default;
            }
        }

    }

    public class RequestManager<T> : RequestManager, IRequestManager<T>
    {
        public RequestManager(ILogger<RequestManager> logger,
            IRedisConnectionProvider<RequestManager> redisConnectionProvider,
            IResponseSerializer serializer)
            : base(logger, redisConnectionProvider, serializer)
        {
        }
    }
}
