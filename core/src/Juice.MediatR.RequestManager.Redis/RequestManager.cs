using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace Juice.MediatR.RequestManager.Redis
{
    public class RequestManager : IRequestManager
    {
        private static RedisOptions configuration;
        /// <summary>  
        /// The lazy connection.  
        /// </summary>  
        private Lazy<ConnectionMultiplexer> lazyConnection = new Lazy<ConnectionMultiplexer>(() =>
        {
            return ConnectionMultiplexer.Connect(configuration.ConnectionString);
        });

        /// <summary>  
        /// Gets the connection.  
        /// </summary>  
        /// <value>The connection.</value>  
        public ConnectionMultiplexer Connection => lazyConnection.Value;

        private readonly ILogger _logger;
        public RequestManager(ILogger<RequestManager> logger, IOptions<RedisOptions> options)
        {
            _logger = logger;
            configuration = options.Value;
        }

        public async Task TryCompleteRequestAsync(Guid id, bool success)
        {
            var key = "IdentifiedCommand:" + id.ToString();

            if (success)
            {
                await Connection.GetDatabase().StringSetAsync(key, "", default, When.Exists);
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
                    var res = Connection.GetDatabase().ScriptEvaluate(lua_script,
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
                    _logger.LogError($"ReleaseLock lock fail...{ex.Message}");
                }
            }
        }
        public async Task<bool> TryCreateRequestForCommandAsync<T, R>(Guid id)
            where T : IRequest<R>
        {
            var key = typeof(T).Name + ":" + id.ToString();
            var flag = await Connection.GetDatabase().StringSetAsync(key, "", TimeSpan.FromMinutes(15), When.NotExists);
            return flag;
        }
    }
}
