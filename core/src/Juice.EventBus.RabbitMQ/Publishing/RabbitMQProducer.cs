using System.Net.Sockets;
using Juice.EventBus.Publishing;
using Juice.Messaging.Diagnostics;
using Microsoft.Extensions.Logging;
using Polly;
using RabbitMQ.Client;
using RabbitMQ.Client.Exceptions;

namespace Juice.EventBus.RabbitMQ.Publishing
{
    internal sealed class RabbitMQProducer : ITransportPublisher, IAsyncDisposable
    {
        public string Key { get; init; }
        private readonly IRabbitMQPersistentConnection _persistentConnection;
        private string _defaultExchange = "default_exchange";
        private readonly ILogger _logger;
        private readonly IAsyncPolicy _retryPolicy;
        private readonly bool _mandatory;

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
            _logger = loggerFactory.CreateLogger(logCategory);
            _mandatory = endpoint.Mandatory;

            // Short retries for a dropped connection or channel only; the outbox owns the long retries.
            _retryPolicy = Policy.Handle<BrokerUnreachableException>()
                .Or<SocketException>()
                .Or<OperationInterruptedException>(IsTransient)
                .WaitAndRetryAsync(endpoint.MaxRetryAttempts,
                    retryAttempt => endpoint.RetryBaseDelay * Math.Pow(2, retryAttempt - 1),
                    (ex, time, context) =>
                    {
                        _logger.LogWarning(ex,
                            "Could not publish event: {EventId}, retrying in {Timeout}s ({ExceptionMessage})",
                            context.TryGetValue("messageId", out var id) ? id : null, $"{time.TotalSeconds:n1}", ex.Message);
                    });

            _channelManager = new ChannelManager(endpoint.PoolCapacity, _persistentConnection, loggerFactory, logCategory);

            _channelManager.ChannelFailureAsync += async (s, e) =>
            {
                RabbitMQMetrics.IncrementChannelError(e.Exchange);
                if (e.ReplyCode == 404) // Not Found
                {
                    _logger.LogError("Exchange not found: {Exchange}. Publishing to it fails until it is declared.", e.Exchange);
                }
            };

        }

        #region Publish outgoing event

        public async ValueTask PublishAsync(byte[] payload, PublishContext context,
            CancellationToken cancellationToken = default)
        {
            if (_disposedValue)
                throw new ObjectDisposedException(nameof(RabbitMQProducer));

            if (!_persistentConnection.IsConnected && !await _persistentConnection.TryConnectAsync(cancellationToken).ConfigureAwait(false))
            {
                throw new InvalidOperationException("RabbitMQ broker is not connected");
            }

            // copy: an outbox delivery passes the stored event headers
            var headers = context.Headers != null
                ? new Dictionary<string, object?>(context.Headers)
                : new Dictionary<string, object?>();
            var messageId = context.MessageId;

            // the route's routing key (publishing policy) wins over the message headers, as for NATS
            var routingKey = (!string.IsNullOrEmpty(context.RoutingKey) ? context.RoutingKey : null)
                ?? headers.GetHeaderString("x-routing-key")
                ?? headers.GetHeaderString("x-message-name")
                ?? headers.GetHeaderString("x-message-type")
                ?? throw new InvalidOperationException(
                    "Missing required header 'x-message-name' or 'x-message-type' for routing key");

            var exchangeName = context.Destination ?? _defaultExchange;
            using var activity = MessagingActivity.StartSend("rabbitmq", exchangeName, routingKey, messageId);
            activity?.SetTag("messaging.rabbitmq.destination.routing_key", routingKey);
            MessagingActivity.Inject(headers, activity);
            headers["x-original-exchange"] = exchangeName;
            headers["x-original-routing-key"] = routingKey;
            try
            {
                await _retryPolicy.ExecuteAsync(async (_, ct) =>
                {
                    if (_disposedValue)
                        throw new ObjectDisposedException(nameof(RabbitMQProducer));

                    var channel = await _channelManager.RentAsync(exchangeName, ct);
                    try
                    {
                        var properties = new BasicProperties
                        {
                            ContentType = "application/json",
                            CorrelationId = headers.GetHeaderString("x-correlation-id"),
                            MessageId = messageId,
                            Timestamp = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds()),
                            DeliveryMode = DeliveryModes.Persistent,
                            Headers = headers.ToStandardized()
                        };

                        if (_logger.IsEnabled(LogLevel.Debug))
                        {
                            _logger.LogDebug("Publishing event to {Exchange}: {EventId} {EventName}",
                                exchangeName, messageId, routingKey);
                        }
                        // the channel tracks publisher confirms: this completes once the broker has the message
                        await channel.BasicPublishAsync(
                            exchange: exchangeName,
                            routingKey: routingKey,
                            mandatory: _mandatory,
                            basicProperties: properties,
                            body: payload,
                            cancellationToken: ct);
                    }
                    finally
                    {
                        await _channelManager.ReturnAsync(exchangeName, channel);
                    }
                }, new Context { ["messageId"] = messageId }, cancellationToken);
                MessagingMetrics.RecordPublished("rabbitmq", exchangeName, routingKey);
            }
            catch (Exception ex)
            {
                var error = Describe(ex, messageId, exchangeName, routingKey);
                MessagingActivity.SetError(activity, error);
                MessagingMetrics.RecordPublishError("rabbitmq", exchangeName, routingKey, error);
                if (ReferenceEquals(error, ex))
                {
                    throw;
                }
                throw error;
            }
        }

        /// <summary>
        /// A channel closed by a hard, deterministic error (the exchange does not exist, access refused,
        /// precondition failed) would fail the same way again; anything else is a dropped connection or channel.
        /// </summary>
        private static bool IsTransient(OperationInterruptedException ex)
            => ex.ShutdownReason?.ReplyCode is not (Constants.NotFound or Constants.AccessRefused or Constants.PreconditionFailed);

        /// <summary>
        /// Gives the broker's refusals a message that names the message and its route; the outbox stores it as the
        /// delivery error.
        /// </summary>
        private static Exception Describe(Exception ex, string messageId, string exchange, string routingKey)
            => ex switch
            {
                PublishException { IsReturn: true } => new InvalidOperationException(
                    $"Message {messageId} is unroutable: exchange '{exchange}' has no queue bound for routing key '{routingKey}'", ex),
                PublishException => new InvalidOperationException(
                    $"Message {messageId} was not confirmed (nack) by the broker for exchange '{exchange}'", ex),
                OperationInterruptedException { ShutdownReason: { ReplyCode: Constants.NotFound } reason } => new InvalidOperationException(
                    $"Message {messageId} was not published: {reason.ReplyText}", ex),
                _ => ex
            };

        #endregion

        public async ValueTask DisposeAsync()
        {
            if (!_disposedValue)
            {
                _disposedValue = true;
                await _channelManager.DisposeAsync();
            }
        }
    }

}
