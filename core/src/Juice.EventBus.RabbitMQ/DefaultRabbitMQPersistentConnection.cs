using System.Net.Sockets;
using Microsoft.Extensions.Logging;
using Polly;
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
        private readonly ILogger _logger;
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
                // the client recovers a lost connection and its channels; this class only makes the first connection
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

        public ValueTask<IChannel?> CreateChannelAsync(CancellationToken cancellationToken = default)
            => CreateChannelAsync(null, cancellationToken);

        public async ValueTask<IChannel?> CreateChannelAsync(CreateChannelOptions? options, CancellationToken cancellationToken = default)
        {
            if (!IsConnected)
            {
                throw new InvalidOperationException("No RabbitMQ connections are available to perform this action");
            }

            return await _connection!.CreateChannelAsync(options, cancellationToken);
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
            await sync_root.WaitAsync(cancellationToken);
            try
            {
                if (IsConnected)
                {
                    return true;
                }
                if (_connection != null)
                {
                    // a connection that has been open once is recovering: a second one would leak
                    _logger.LogWarning("RabbitMQ connection is recovering; it is not available yet");
                    return false;
                }
                _logger.LogInformation("RabbitMQ Client is trying to connect");
                var policy = Policy.Handle<SocketException>()
                        .Or<BrokerUnreachableException>()
                        .WaitAndRetryAsync((int)_retryCount, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)), (ex, time) =>
                        {
                            _logger.LogWarning(ex, "RabbitMQ Client could not connect after {TimeOut}s ({ExceptionMessage})", $"{time.TotalSeconds:n1}", ex.Message);
                        }
                    );

                IConnection connection;
                try
                {
                    connection = await policy.ExecuteAsync(ct => _connectionFactory.CreateConnectionAsync(ct), cancellationToken);
                }
                catch (Exception ex) when (ex is SocketException or BrokerUnreachableException)
                {
                    _logger.LogCritical(ex, "FATAL ERROR: RabbitMQ connections could not be created and opened");
                    return false;
                }

                connection.ConnectionShutdownAsync += OnConnectionShutdownAsync;
                connection.CallbackExceptionAsync += OnCallbackExceptionAsync;
                connection.ConnectionBlockedAsync += OnConnectionBlockedAsync;
                connection.RecoverySucceededAsync += OnRecoverySucceededAsync;
                connection.ConnectionRecoveryErrorAsync += OnConnectionRecoveryErrorAsync;

                _connection = connection;
                _logger.LogInformation("RabbitMQ Client acquired a persistent connection to '{HostName}' and is subscribed to failure events", connection.Endpoint.HostName);

                return true;
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

        private Task OnCallbackExceptionAsync(object? sender, CallbackExceptionEventArgs e)
        {
            if (_disposed) { return Task.CompletedTask; }

            _logger.LogWarning(e.Exception, "A RabbitMQ connection callback threw an exception.");
            return Task.CompletedTask;
        }

        private Task OnConnectionShutdownAsync(object? sender, ShutdownEventArgs reason)
        {
            if (_disposed || _disposing) { return Task.CompletedTask; }

            _logger.LogWarning("A RabbitMQ connection is on shutdown ({ReplyCode} {ReplyText}). Waiting for automatic recovery...",
                reason.ReplyCode, reason.ReplyText);
            return Task.CompletedTask;
        }

        private Task OnRecoverySucceededAsync(object? sender, AsyncEventArgs e)
        {
            if (_disposed) { return Task.CompletedTask; }

            _logger.LogInformation("RabbitMQ connection recovered.");
            return Task.CompletedTask;
        }

        private Task OnConnectionRecoveryErrorAsync(object? sender, ConnectionRecoveryErrorEventArgs e)
        {
            if (_disposed) { return Task.CompletedTask; }

            _logger.LogWarning(e.Exception, "RabbitMQ connection recovery attempt failed; the client keeps retrying.");
            return Task.CompletedTask;
        }

        public async ValueTask DisconnectAsync()
        {
            if (_disposed || _disposing)
            {
                _logger.LogInformation("Disconnect bypassed because RabbitMQ Client is disposed");
                return;
            }
            var connection = _connection;
            if (connection != null)
            {
                connection.ConnectionShutdownAsync -= OnConnectionShutdownAsync;
                connection.CallbackExceptionAsync -= OnCallbackExceptionAsync;
                connection.ConnectionBlockedAsync -= OnConnectionBlockedAsync;
                connection.RecoverySucceededAsync -= OnRecoverySucceededAsync;
                connection.ConnectionRecoveryErrorAsync -= OnConnectionRecoveryErrorAsync;
                // a closed connection does not recover: drop it so the next TryConnectAsync opens a new one
                _connection = null;
                await connection.CloseAsync();
                connection.Dispose();
                _logger.LogInformation("RabbitMQ Client is disconnected.");
            }
        }
    }
}
