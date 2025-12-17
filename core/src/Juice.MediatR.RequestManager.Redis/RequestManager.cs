using Juice.Extensions.Redis;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
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

        private readonly ILogger _logger;
        public RequestManager(ILogger<RequestManager> logger, IRedisConnectionProvider<RequestManager> redisConnectionProvider)
        {
            _logger = logger;
            ConnectionProvider = redisConnectionProvider;
        }

        private string GetKey<T>(Guid id)
        {
            return typeof(T).Name + ":" + id.ToString();
        }

        public async ValueTask TryCompleteRequestAsync<T>(Guid id, bool success)
            where T : IBaseRequest
        {
            var key = GetKey<T>(id);
            var connection = await ConnectionProvider.GetConnectionAsync();

            if (success)
            {
                await connection.GetDatabase().StringSetAsync(key, DateTimeOffset.Now.ToString(), default, When.Exists);
            }
            else
            {
                string lua_script = @"  
                if (redis.call('GET', KEYS[1]) == ARGV[1]) then  
                    redis.call('DEL', KEYS[1])  
                    return true  
                else  
                    return false  
                end  
                ";

                try
                {
                    var res = connection.GetDatabase().ScriptEvaluate(lua_script,
                                                               new RedisKey[] { key },
                                                               new RedisValue[] { "" });
                    var ok = (bool)res;
                    if (!ok)
                    {
                        _logger.LogError($"Failed to evaluate script");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error while trying to complete request for command {CommandId}. {Message}", id, ex.Message);
                }
            }
        }

        public async ValueTask<bool> TryCreateRequestForCommandAsync<T>(Guid id)
            where T : IBaseRequest
        {
            var key = GetKey<T>(id);
            var connection = await ConnectionProvider.GetConnectionAsync();

            var flag = await connection.GetDatabase().StringSetAsync(key, "", TimeSpan.FromMinutes(15), When.NotExists);
            return flag;
        }
    }
    public class RequestManager<T> : RequestManager, IRequestManager<T>
    {
        public RequestManager(ILogger<RequestManager> logger, IRedisConnectionProvider<RequestManager> redisConnectionProvider)
            : base(logger, redisConnectionProvider)
        {
        }
    }
}
