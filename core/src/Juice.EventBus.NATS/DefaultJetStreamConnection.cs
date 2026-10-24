using System.Reflection;
using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using NATS.Client.JetStream;

namespace Juice.EventBus.NATS
{
    internal sealed class DefaultJetStreamConnection : IJetStreamConnection, IAsyncDisposable, IDisposable
    {
        public string Name { get; }

        private readonly NatsOpts _opts;
        private readonly ILogger _logger;
        private readonly object _lock = new();
        private NatsConnection? _connection;
        private NatsJSContext? _jetStream;
        private volatile bool _disposed;

        public DefaultJetStreamConnection(string name, JetStreamConnectionOptions options, ILogger logger)
        {
            Name = name;
            _logger = logger;
            _opts = BuildNatsOpts(name, options, logger);
        }

        public INatsConnection Connection => GetConnection();

        public INatsJSContext JetStream
        {
            get
            {
                var connection = GetConnection();
                lock (_lock)
                {
                    return _jetStream ??= new NatsJSContext(connection);
                }
            }
        }

        public NatsConnectionState State => _connection?.ConnectionState ?? NatsConnectionState.Closed;

        public async ValueTask EnsureConnectedAsync(CancellationToken cancellationToken = default)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(DefaultJetStreamConnection));

            var connection = GetConnection();
            if (connection.ConnectionState != NatsConnectionState.Open)
            {
                await connection.ConnectAsync().AsTask().WaitAsync(cancellationToken).ConfigureAwait(false);
            }
        }

        private NatsConnection GetConnection()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(DefaultJetStreamConnection));

            lock (_lock)
            {
                if (_connection != null)
                {
                    return _connection;
                }
                var connection = new NatsConnection(_opts);
                connection.ConnectionOpened += (_, e) =>
                {
                    _logger.LogInformation("[{Name}] JetStream connection opened. {Message}", Name, e.Message);
                    return default;
                };
                connection.ConnectionDisconnected += (_, e) =>
                {
                    _logger.LogWarning("[{Name}] JetStream connection lost. Reconnecting... {Message}", Name, e.Message);
                    return default;
                };
                connection.ReconnectFailed += (_, e) =>
                {
                    _logger.LogWarning("[{Name}] JetStream reconnect failed. {Message}", Name, e.Message);
                    return default;
                };
                connection.MessageDropped += (_, e) =>
                {
                    _logger.LogWarning("[{Name}] Message dropped by the client on {Subject} ({Pending} pending). It will be redelivered after AckWait.",
                        Name, e.Subject, e.Pending);
                    return default;
                };
                _connection = connection;
                return connection;
            }
        }

        internal static NatsOpts BuildNatsOpts(string name, JetStreamConnectionOptions options, ILogger logger)
        {
            options.Validate(name);

            var tls = options.Tls ?? new JetStreamTlsOptions();
            if (tls.InsecureSkipVerify)
            {
                logger.LogWarning("[{Name}] TLS server certificate verification is disabled (InsecureSkipVerify). Do not use this in production.", name);
            }

            return new NatsOpts
            {
                Url = options.Url,
                Name = !string.IsNullOrWhiteSpace(options.Name)
                    ? options.Name!
                    : $"{Assembly.GetEntryAssembly()?.GetName().Name}-{name}",
                AuthOpts = new NatsAuthOpts
                {
                    Username = options.UserName,
                    Password = options.Password,
                    Token = options.Token,
                    CredsFile = options.CredsFile,
                    NKeyFile = options.NKeySeedFile
                },
                TlsOpts = new NatsTlsOpts
                {
                    Mode = tls.Mode switch
                    {
                        JetStreamTlsMode.Prefer => TlsMode.Prefer,
                        JetStreamTlsMode.Require => TlsMode.Require,
                        JetStreamTlsMode.Implicit => TlsMode.Implicit,
                        JetStreamTlsMode.Disable => TlsMode.Disable,
                        _ => TlsMode.Auto
                    },
                    CaFile = tls.CaFile,
                    CertFile = tls.CertFile,
                    KeyFile = tls.KeyFile,
                    InsecureSkipVerify = tls.InsecureSkipVerify
                },
                ConnectTimeout = options.ConnectTimeout,
                ReconnectWaitMin = options.ReconnectWaitMin,
                ReconnectWaitMax = options.ReconnectWaitMax,
                MaxReconnectRetry = options.MaxReconnectRetry
            };
        }

        public async ValueTask DisposeAsync()
        {
            if (_disposed)
            {
                return;
            }
            _disposed = true;
            if (_connection != null)
            {
                await _connection.DisposeAsync();
            }
        }

        public void Dispose()
        {
            DisposeAsync().AsTask().GetAwaiter().GetResult();
        }
    }
}
