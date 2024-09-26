using System.Net.Sockets;
using System.Threading;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;
using Polly.Retry;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.Exceptions;
using IConnectionFactory = RabbitMQ.Client.IConnectionFactory;

namespace Juice.EventBus.RabbitMQ
{
    public class DefaultRabbitMQPersistentConnection
           : IRabbitMQPersistentConnection
    {
        private IConnectionFactory _connectionFactory;
        private ILogger<DefaultRabbitMQPersistentConnection> _logger;
        private readonly int _retryCount;
        private IConnection? _connection;
        private bool _disposed;
        private bool _disposing;

        private Lock sync_root = new();

        public DefaultRabbitMQPersistentConnection(IOptions<RabbitMQOptions> optionsAccessor,
            ILogger<DefaultRabbitMQPersistentConnection> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            var options = optionsAccessor.Value;
            var factory = new ConnectionFactory()
            {
                HostName = options.Connection,
                DispatchConsumersAsync = true
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
            _retryCount = options.RetryCount;
        }

        public bool IsConnected
        {
            get
            {
                return _connection != null && _connection.IsOpen && !_disposed;
            }
        }

        public IModel? CreateModel()
        {
            if (!IsConnected)
            {
                throw new InvalidOperationException("No RabbitMQ connections are available to perform this action");
            }

            return _connection?.CreateModel();
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
                    _connection?.Close();
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

        public bool TryConnect()
        {
            if (_disposed || _disposing) {
                _logger.LogInformation("Connect bypassed because RabbitMQ Client is disposed");
                return false;
            }
            
            _logger.LogInformation("RabbitMQ Client is trying to connect");

            lock (sync_root)
            {
                var policy = RetryPolicy.Handle<SocketException>()
                    .Or<BrokerUnreachableException>()
                    .WaitAndRetry(_retryCount, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)), (ex, time) =>
                    {
                        _logger.LogWarning(ex, "RabbitMQ Client could not connect after {TimeOut}s ({ExceptionMessage})", $"{time.TotalSeconds:n1}", ex.Message);
                    }
                );

                policy.Execute(() =>
                {
                    _connection = _connectionFactory
                          .CreateConnection();
                });

                if (IsConnected)
                {
                    _connection.ConnectionShutdown += OnConnectionShutdown;
                    _connection.CallbackException += OnCallbackException;
                    _connection.ConnectionBlocked += OnConnectionBlocked;

                    _logger.LogInformation("RabbitMQ Client acquired a persistent connection to '{HostName}' and is subscribed to failure events", _connection.Endpoint.HostName);

                    return true;
                }
                else
                {
                    _logger.LogCritical("FATAL ERROR: RabbitMQ connections could not be created and opened");

                    return false;
                }
            }
        }

        private void OnConnectionBlocked(object sender, ConnectionBlockedEventArgs e)
        {
            if (_disposed) { return; }

            _logger.LogWarning("A RabbitMQ connection is shutdown. Trying to re-connect...");

            TryConnect();
        }

        private void OnCallbackException(object sender, CallbackExceptionEventArgs e)
        {
            if (_disposed) { return; }

            _logger.LogWarning("A RabbitMQ connection throw exception. Trying to re-connect...");

            TryConnect();
        }

        private void OnConnectionShutdown(object sender, ShutdownEventArgs reason)
        {
            if (_disposed || _disposing) { return; }

            _logger.LogWarning("A RabbitMQ connection is on shutdown. Trying to re-connect...");

            TryConnect();
        }
    }
}
