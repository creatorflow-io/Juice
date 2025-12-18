using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace Juice.Extensions.Redis.Internal
{
    internal class RedisConnectionProvider : IAsyncDisposable, IRedisConnectionProvider
    {
        private readonly RedisOptions _options;
        private readonly ILogger _logger;
        private readonly SemaphoreSlim _lock = new(1, 1);

        private ConnectionMultiplexer? _sentinel;
        private IConnectionMultiplexer? _master;

        public RedisConnectionProvider(
            IOptions<RedisOptions> options,
            ILogger<RedisConnectionProvider> logger): this(options.Value, logger)
        {
        }

        protected RedisConnectionProvider(
            RedisOptions options,
            ILogger logger)
        {
            options.Validate();
            _options = options;
            _logger = logger;
        }

        public async Task<IConnectionMultiplexer> GetConnectionAsync()
        {
            await _lock.WaitAsync();
            try
            {
                if (_master is { IsConnected: true })
                {
                    return _master;
                }

                if (string.IsNullOrEmpty(_options.ConnectionString))
                {
                    throw new InvalidOperationException(
                        "Redis connection string is not configured.");
                }

                if(!_options.UseSentinel)
                {
                    _master?.Dispose();
                    _master = await ConnectionMultiplexer.ConnectAsync(_options.ConnectionString);

                    _logger.LogInformation(
                        "Connected to Redis master via Connection String.");
                    return _master;
                }

                _sentinel ??= await ConnectionMultiplexer.SentinelConnectAsync(_options.ConnectionString!);

                _master?.Dispose();
                _master =   _sentinel.GetSentinelMasterConnection(CreateMasterOptions());
                _logger.LogInformation("Connected to Redis master via Sentinel");

                if (_logger.IsEnabled(LogLevel.Debug))
                {
                    var endPoints = _master.GetEndPoints();

                    foreach (var ep in endPoints)
                    {
                        var info = await _master.GetServer(ep).InfoAsync("replication");
                        var role = info.First().First(x => x.Key == "role").Value;
                        _logger.LogInformation("Redis endpoint {Endpoint}, role = {Role}", ep.ToString(), role);
                    }
                }

                return _master;
            }
            finally
            {
                _lock.Release();
            }
        }


        private ConfigurationOptions CreateMasterOptions()
        {
            var masterOptions = new ConfigurationOptions
            {
                ServiceName = _options.SentinelMasterName,
                Password = _options.Password,
                AbortOnConnectFail = false,
                AllowAdmin = _options.AllowAdmin || _logger.IsEnabled(LogLevel.Debug) // Allow admin commands in debug mode for inspection
            };
            
            return masterOptions;
        }

        public async ValueTask DisposeAsync()
        {
            _master?.Dispose();
            _sentinel?.Dispose();
            _lock.Dispose();
            await Task.CompletedTask;
        }
    }

    internal class RedisConnectionProvider<T> : RedisConnectionProvider, IRedisConnectionProvider<T>
    {
        public RedisConnectionProvider(
            IOptions<RedisOptions<T>> options,
            ILogger<RedisConnectionProvider> logger)
            : base(options.Value, logger)
        {
        }
    }
}
