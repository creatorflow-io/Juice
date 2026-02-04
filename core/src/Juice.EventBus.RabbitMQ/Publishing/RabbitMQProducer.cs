using System.Net.Sockets;
using System.Text.Json;
using Juice.EventBus.Publishing;
using Microsoft.Extensions.Logging;
using Polly;
using RabbitMQ.Client;
using RabbitMQ.Client.Exceptions;

namespace Juice.EventBus.RabbitMQ.Publishing
{
    internal sealed class RabbitMQProducer : IEventPublisher, IAsyncDisposable
    {
        public string Key { get; init; }
        private readonly IRabbitMQPersistentConnection _persistentConnection;
        private string _defaultExchange = "default_exchange";
        private readonly int _retryCount;
        private readonly ILogger _logger;

        private readonly ChannelManager _channelManager;

        private volatile bool _disposedValue;
        public RabbitMQProducer(
            ILoggerFactory loggerFactory,
            IRabbitMQPersistentConnection mQPersistentConnection,
            RabbitMQProducerEndpoint endpoint
            )
        {
            Key = endpoint.Key;

            _persistentConnection = mQPersistentConnection;

            var logCategory = typeof(RabbitMQProducer).FullName + $"[{endpoint.Key}]";

            if (!string.IsNullOrEmpty(endpoint.DefaultExchange))
            {
                _defaultExchange = endpoint.DefaultExchange;
            }
            _retryCount = endpoint.MaxRetryAttempts;
            _logger = loggerFactory.CreateLogger(logCategory);

            _channelManager = new ChannelManager(endpoint.PoolCapacity, _persistentConnection, loggerFactory, logCategory);

            _channelManager.ChannelFailureAsync += async (s, e) =>
            {
                RabbitMQMetrics.IncrementChannelError(e.Exchange);
                if (e.ReplyCode == 404) // Not Found
                {
                    _logger.LogError("DANGER: Exchange not found: {Exchange}. The messages maybe loss.", e.Exchange);
                }
            };

        }

        #region Publish outgoing event

        public async ValueTask PublishAsync<T>(T @event, PublishContext? context = default,
            CancellationToken cancellationToken = default)
            where T : IIntegrationEvent
        {
            ArgumentNullException.ThrowIfNull(@event);

            if (_disposedValue)
                throw new ObjectDisposedException(nameof(RabbitMQProducer));

            if (!_persistentConnection.IsConnected && !await _persistentConnection.TryConnectAsync(cancellationToken).ConfigureAwait(false))
            {
                throw new InvalidOperationException("RabbitMQ broker is not connected");
            }

            // Validate tenant
            var tenantId = context?.TenantId;
            if (!string.IsNullOrEmpty(tenantId) && @event.TenantId != null
                && !@event.TenantId.Equals(tenantId, StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException("TenantId is not match with event tenantId", nameof(tenantId));
            }
            tenantId ??= @event.TenantId;

            var policy = Policy.Handle<BrokerUnreachableException>()
                .Or<SocketException>()
                 .Or<OperationInterruptedException>()
                .WaitAndRetry((int)_retryCount,
                    retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                    (ex, time) =>
                    {
                        _logger.LogWarning(ex,
                            "Could not publish event: {EventId} after {Timeout}s ({ExceptionMessage})",
                            @event.Id, $"{time.TotalSeconds:n1}", ex.Message);
                    });

            var eventName = @event.GetEventKey();

            var body = JsonSerializer.SerializeToUtf8Bytes(@event, @event.GetType(),
                new JsonSerializerOptions { WriteIndented = true });

            await policy.Execute(async (ct) =>
            {
                if (_disposedValue)
                    throw new ObjectDisposedException(nameof(RabbitMQProducer));

                IChannel? channel = null;
                var exchange = context?.Destination ?? _defaultExchange;

                try
                {
                    channel = await _channelManager.RentAsync(exchange, ct);

                    var properties = new BasicProperties
                    {
                        ContentType = "application/json",
                        CorrelationId = @event.Id.ToString(),
                        MessageId = Guid.NewGuid().ToString(),
                        Timestamp = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds()),
                        DeliveryMode = DeliveryModes.Persistent,
                        Headers = new Dictionary<string, object?>()
                        {
                            { "x-tenant-id", tenantId ?? string.Empty },
                            { "x-original-exchange", exchange },
                            { "x-event-type", @event.GetType().FullName }
                        }
                    };

                    if (_logger.IsEnabled(LogLevel.Debug))
                    {
                        _logger.LogDebug("Publishing event to {Exchange}: {EventId} {EventName}",
                            exchange, @event.Id, eventName);
                    }
                    await channel.BasicPublishAsync(
                        exchange: exchange,
                        routingKey: eventName,
                        mandatory: true,
                        basicProperties: properties,
                        body: body,
                        cancellationToken: ct);
                }
                finally
                {
                    await _channelManager.ReturnAsync(exchange, channel);
                }
            }, cancellationToken);
        }

        #endregion

        public async ValueTask DisposeAsync()
        {
            if (!_disposedValue)
            {
                await _channelManager.DisposeAsync();
                _disposedValue = true;
            }
        }
    }

}
