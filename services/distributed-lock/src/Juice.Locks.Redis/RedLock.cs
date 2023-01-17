using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace Juice.Locks.Redis
{
    public class RedLock : IDistributedLock
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
        public RedLock(ILogger<RedLock> logger, IOptions<RedisOptions> options)
        {
            _logger = logger;
            configuration = options.Value;
        }

        public ILock? AcquireLock(string key, string issuer, TimeSpan expiration)
        {
            try
            {
                if (key == null)
                {
                    throw new ArgumentNullException(nameof(key));
                }

                var flag = Connection.GetDatabase().StringSet(key, issuer ?? "", expiration, When.NotExists);
                if (flag || Connection.GetDatabase().StringGet(key).ToString() == issuer)
                {
                    return new Lock(this, key, issuer ?? "");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Acquire lock fail...{ex.Message}");
            }
            return default;
        }

        public async Task<ILock?> AcquireLockAsync(string key, string issuer, TimeSpan expiration)
        {
            try
            {
                if (key == null)
                {
                    throw new ArgumentNullException(nameof(key));
                }

                var flag = await Connection.GetDatabase().StringSetAsync(key, issuer ?? "", expiration, When.NotExists);
                if (flag || (issuer != "" && (await Connection.GetDatabase().StringGetAsync(key)).ToString() == issuer))
                {
                    return new Lock(this, key, issuer ?? "");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Acquire lock fail...{ex.Message}");
            }
            return default;
        }

        public bool ReleaseLock(ILock @lock)
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
                                                           new RedisKey[] { @lock.Key },
                                                           new RedisValue[] { @lock.Value });
                var ok = (bool)res;
                if (ok)
                {
                    _logger.LogInformation($"ReleaseLock {@lock.Key} {@lock.Value}");
                    return true;
                }
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError($"ReleaseLock lock fail...{ex.Message}");
                return false;
            }
        }

        public async Task<bool> ReleaseLockAsync(ILock @lock)
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
                var res = await Connection.GetDatabase().ScriptEvaluateAsync(lua_script,
                                                           new RedisKey[] { @lock.Key },
                                                           new RedisValue[] { @lock.Value });
                var ok = (bool)res;
                if (ok)
                {
                    _logger.LogInformation($"ReleaseLock {@lock.Key} {@lock.Value}");
                    return true;
                }
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError($"ReleaseLock lock fail...{ex.Message}");
                return false;
            }
        }
    }
}
