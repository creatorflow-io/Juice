using System.Net;
using Juice.Extensions.Options;
using Microsoft.Extensions.Logging;
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
            IOptionsProvider<IRedisConnectionProvider, RedisOptions> options,
            ILogger<RedisConnectionProvider> logger): this(options.Value, logger)
        {
        }

        protected RedisConnectionProvider(
            RedisOptions options,
            ILogger logger)
        {
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

                var options = CreateMasterOptions();

                if (!IsSentinel(options))
                {
                    _master?.Dispose();
                    _master = await ConnectionMultiplexer.ConnectAsync(options);

                    _logger.LogInformation(
                        "Connected to Redis master via Connection String.");
                    return _master;
                }

                _sentinel ??= await ConnectionMultiplexer.SentinelConnectAsync(options);

                _master?.Dispose();
                _master = _sentinel.GetSentinelMasterConnection(options);
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
            var options = ConfigurationOptions.Parse(_options.ConnectionString!);

            options.AllowAdmin = options.AllowAdmin
                || (_logger.IsEnabled(LogLevel.Debug) && IsSentinel(options)); // Allow admin commands in debug mode for inspection
            return options;
        }

        private static bool IsSentinel(ConfigurationOptions options)
        {
            return
                options.ServiceName != null ||
                options.CommandMap == CommandMap.Sentinel ||
                options.EndPoints.Any(ep =>
                    ep is IPEndPoint ip && ip.Port == 26379 ||
                    ep is DnsEndPoint dns && dns.Port == 26379
                );
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
            IOptionsProvider<IRedisConnectionProvider<T>, RedisOptions> options,
            ILogger<RedisConnectionProvider> logger)
            : base(options.Value, logger)
        {
        }
    }
}
