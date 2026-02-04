using System.Net.Sockets;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Retry;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.Exceptions;
using IConnectionFactory = RabbitMQ.Client.IConnectionFactory;

namespace Juice.EventBus.RabbitMQ
{
    internal class DefaultRabbitMQPersistentConnection
           : IRabbitMQPersistentConnection
    {
        public string Name { get; init; }
        private IConnectionFactory _connectionFactory;
        private ILogger _logger;
        private readonly uint _retryCount;
        private IConnection? _connection;
        private bool _disposed;
        private bool _disposing;

        private SemaphoreSlim sync_root = new SemaphoreSlim(1, 1);

        public DefaultRabbitMQPersistentConnection(string name, RabbitMQConnectionOptions options, ILogger logger)
        {
            Name = name;
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            var factory = new ConnectionFactory()
            {
                HostName = options.Connection!,
                AutomaticRecoveryEnabled = true,
                NetworkRecoveryInterval = TimeSpan.FromSeconds(5)
            };
            if (!string.IsNullOrEmpty(options.UserName))
            {
                factory.UserName = options.UserName;
                if (!string.IsNullOrEmpty(options.Password))
                {
                    factory.Password = options.Password;
                }
            }
            if (!string.IsNullOrEmpty(options.VirtualHost))
            {
                factory.VirtualHost = options.VirtualHost;
            }
            if (options.Port > 0)
            {
                factory.Port = options.Port;
            }
            _connectionFactory = factory;
            _retryCount = options.ConnectionMaxRetries;
        }

        public bool IsConnected
        {
            get
            {
                return _connection != null && _connection.IsOpen && !_disposed;
            }
        }

        public async ValueTask<IChannel?> CreateChannelAsync(CancellationToken cancellationToken = default)
        {
            if (!IsConnected)
            {
                throw new InvalidOperationException("No RabbitMQ connections are available to perform this action");
            }

            return await _connection!.CreateChannelAsync();
        }
        #region Disposable

        // Public implementation of Dispose pattern callable by consumers.
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        // Protected implementation of Dispose pattern.
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    _disposing = true;
                    // dispose managed state (managed objects)
                    _connection?.Dispose();
                    _connection = null!;
                    _logger = null!;
                    _connectionFactory = null!;
                }

                // free unmanaged resources (unmanaged objects) and override finalizer
                // set large fields to null

                _disposed = true;
            }
        }
        #endregion

        public async ValueTask<bool> TryConnectAsync(CancellationToken cancellationToken)
        {
            if (_disposed || _disposing)
            {
                _logger.LogInformation("Connect bypassed because RabbitMQ Client is disposed");
                return false;
            }
            try
            {
                await sync_root.WaitAsync(cancellationToken);
                if (IsConnected)
                {
                    return true;
                }
                _logger.LogInformation("RabbitMQ Client is trying to connect");
                var policy = RetryPolicy.Handle<SocketException>()
                        .Or<BrokerUnreachableException>()
                        .WaitAndRetry((int)_retryCount, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)), (ex, time) =>
                        {
                            _logger.LogWarning(ex, "RabbitMQ Client could not connect after {TimeOut}s ({ExceptionMessage})", $"{time.TotalSeconds:n1}", ex.Message);
                        }
                    );

                IConnection? connection = null;
                await policy.Execute(async () =>
                    {
                        connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
                    });

                if (connection != null)
                {
                    connection.ConnectionShutdownAsync += OnConnectionShutdownAsync;
                    connection.CallbackExceptionAsync += OnCallbackExceptionAsync;
                    connection.ConnectionBlockedAsync += OnConnectionBlockedAsync;

                    _connection = connection;
                    _logger.LogInformation("RabbitMQ Client acquired a persistent connection to '{HostName}' and is subscribed to failure events", connection.Endpoint.HostName);

                    return true;
                }
                else
                {
                    _logger.LogCritical("FATAL ERROR: RabbitMQ connections could not be created and opened");

                    return false;
                }
            }
            finally
            {
                sync_root.Release();
            }
        }

        private Task OnConnectionBlockedAsync(object? sender, ConnectionBlockedEventArgs e)
        {
            if (_disposed) { return Task.CompletedTask; }

            _logger.LogWarning("A RabbitMQ connection is blocked.");
            return Task.CompletedTask;
        }

        private async Task OnCallbackExceptionAsync(object? sender, CallbackExceptionEventArgs e)
        {
            if (_disposed) { return; }

            _logger.LogWarning("A RabbitMQ connection throw exception. Trying to re-connect...");

            await TryConnectAsync(default);
        }

        private async Task OnConnectionShutdownAsync(object? sender, ShutdownEventArgs reason)
        {
            if (_disposed || _disposing) { return; }

            _logger.LogWarning("A RabbitMQ connection is on shutdown. Trying to re-connect...");

            await TryConnectAsync(default);
        }

        public async ValueTask DisconnectAsync()
        {
            if (_disposed || _disposing)
            {
                _logger.LogInformation("Disconnect bypassed because RabbitMQ Client is disposed");
                return;
            }
            if (_connection != null)
            {
                _connection.ConnectionShutdownAsync -= OnConnectionShutdownAsync;
                _connection.CallbackExceptionAsync -= OnCallbackExceptionAsync;
                _connection.ConnectionBlockedAsync -= OnConnectionBlockedAsync;
                await _connection.CloseAsync();
                _logger.LogInformation("RabbitMQ Client is disconnected.");
            }
        }
    }
}
