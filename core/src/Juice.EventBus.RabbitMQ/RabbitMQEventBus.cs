using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Juice.MultiTenant;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Polly;
using Polly.Retry;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.Exceptions;

namespace Juice.EventBus.RabbitMQ
{
    internal class RabbitMQEventBus : IEventBus, IDisposable
    {
        private IRabbitMQPersistentConnection _persistentConnection;

        private IChannel? _consumerChannel;
        private string _queueName;
        private string? _queueType; // classic (default), quorum

        private string _exchange = "default_exchange";
        private string ExchangeRetry => $"{_exchange}.retry";
        private string _exchangeType; // direct, fanout, topic, headers
        private long _ttl; // Time to live in milliseconds for messages in the queue

        private IChannel? _producerChannel;
        private readonly int _retryCount;
        private int _maxProcessRetries; // max retry count for processing event
        private int _processRetryDelayMs;

        protected IEventBusSubscriptionsManager SubsManager { get; }
        protected ILogger Logger { get; }
        private IServiceScopeFactory _scopeFactory;

        public RabbitMQEventBus(IEventBusSubscriptionsManager subscriptionsManager,
            IServiceScopeFactory scopeFactory,
            ILogger logger,
            IRabbitMQPersistentConnection mQPersistentConnection,
            RabbitMQOptions options
            )
        {
            _persistentConnection = mQPersistentConnection;

            _queueName = options.SubscriptionClientName ?? string.Empty;
            _queueType = options.QueueType; // classic, quorum
            _exchangeType = options.ExchangeType ?? "direct";
            _ttl = options.TTL; // default ttl in queue level
            if (!string.IsNullOrEmpty(options.BrokerName))
            {
                _exchange = options.BrokerName;
            }
            _scopeFactory = scopeFactory;
            _retryCount = options.RetryCount;
            _maxProcessRetries = options.ProcessMaxRetries;
            _processRetryDelayMs = options.ProcessRetryDelayMs;

            Logger = logger;
            SubsManager = subscriptionsManager;
            SubsManager.OnEventRemoved += OnEventRemoved;
        }

        private void OnEventRemoved(object? sender, string e)
        {
            DoInternalUnsubscriptionAsync(sender, e).GetAwaiter().GetResult();
        }

        #region Init consume channel and processing incoming event

        private async Task Consumer_ReceivedAsync(object sender, BasicDeliverEventArgs eventArgs)
        {
            var eventName = eventArgs.RoutingKey;
            var message = Encoding.UTF8.GetString(eventArgs.Body.Span);

            try
            {
                if (message.ToLowerInvariant().Contains("throw-fake-exception"))
                {
                    throw new InvalidOperationException($"Fake exception requested: \"{message}\"");
                }

                var (processed, ok) = await ProcessingEventAsync(eventArgs, eventName, message);
                if (_consumerChannel == null)
                {
                    return;
                }
                if (ok)
                {

                    // Even on exception we take the message off the queue.
                    // in a REAL WORLD app this should be handled with a Dead Letter _exchange (DLX). 
                    // For more information see: https://www.rabbitmq.com/dlx.html
                    await _consumerChannel.BasicAckAsync(eventArgs.DeliveryTag, multiple: false);
                }
                else if (!processed || _maxProcessRetries <= 0) // if no handler found for event or processing failed and no retries are allowed
                {
                    Logger.LogWarning("No handler found for RabbitMQ event: {EventName}", eventName);
                    await _consumerChannel.BasicNackAsync(eventArgs.DeliveryTag, multiple: false, requeue: false);
                }
                else // if processing failed
                {
                    var headers = eventArgs.BasicProperties.Headers;
                    headers ??= new Dictionary<string, object?>();
                    int attempts = headers.ContainsKey("x-attempts") ? int.Parse(headers["x-attempts"]?.ToString() ?? "0") : 0;
                    if (attempts >= _maxProcessRetries)
                    {
                        if (Logger.IsEnabled(LogLevel.Debug))
                        {
                            Logger.LogDebug("Max processing retries reached for event: {EventName}, attempts: {Attempts}", eventName, attempts);
                        }
                        await _consumerChannel.BasicNackAsync(eventArgs.DeliveryTag, multiple: true, requeue: false);
                    }
                    else
                    {
                        headers["x-attempts"] = attempts + 1;
                        if (Logger.IsEnabled(LogLevel.Debug))
                        {
                            Logger.LogDebug("Retrying event: {EventName}, attempts: {Attempts}", eventName, headers["x-attempts"]);
                        }
                        // re-publish with updated header
                        await _consumerChannel.BasicPublishAsync(
                            exchange: ExchangeRetry,
                            routingKey: eventName,
                            mandatory: true,
                            basicProperties: new BasicProperties
                            {
                                ContentType = eventArgs.BasicProperties.ContentType,
                                CorrelationId = eventArgs.BasicProperties.CorrelationId,
                                MessageId = eventArgs.BasicProperties.MessageId,
                                Timestamp = eventArgs.BasicProperties.Timestamp,
                                DeliveryMode = DeliveryModes.Persistent,
                                Headers = headers
                            },
                            body: eventArgs.Body

                            );
                        await _consumerChannel.BasicAckAsync(eventArgs.DeliveryTag, multiple: false);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "----- ERROR Processing message \"{0}\". {1}", message, ex.Message);
                Logger.LogTrace(ex.StackTrace);
            }

        }

        private async Task StartBasicConsumeAsync()
        {
            Logger.LogInformation("Starting RabbitMQ basic consume queue {queueName}.", _queueName);

            if (_consumerChannel != null)
            {
                var consumer = new AsyncEventingBasicConsumer(_consumerChannel);

                consumer.ReceivedAsync += Consumer_ReceivedAsync;

                await _consumerChannel.BasicConsumeAsync(
                    queue: _queueName,
                    autoAck: false,
                    consumer: consumer);

                await _consumerChannel.BasicQosAsync(0, 1, false);
            }
            else
            {
                Logger.LogError("StartBasicConsume can't call on _consumerChannel == null");
            }
        }

        /// <summary>
        /// Init exchanges and queues
        /// </summary>
        /// <returns></returns>
        private async ValueTask<IChannel?> CreateConsumerChannelAsync()
        {
            if (!_persistentConnection.IsConnected && !await _persistentConnection.TryConnectAsync())
            {
                return null;
            }

            Logger.LogInformation("Creating RabbitMQ consumer channel. Broker: {Broker}.", _exchange);

            var channel = await _persistentConnection.CreateChannelAsync();
            if (channel == null) { return null; }

            await channel.ExchangeDeclareAsync(exchange: _exchange,
                                    type: _exchangeType);

            if (Logger.IsEnabled(LogLevel.Debug))
            {
                Logger.LogDebug("Declaring RabbitMQ exchange: {ExchangeName} with type: {ExchangeType}", _exchange, _exchangeType);
            }

            var queueArguments = new Dictionary<string, object>
            {
                ["x-message-ttl"] = _ttl // default ttl in queue level
            };
            if (_queueType != null)
            {
                queueArguments["x-queue-type"] = _queueType; // classic, quorum
            }
            QueueDeclareOk queuDeclareOk = await channel.QueueDeclareAsync(queue: _queueName,
                                 durable: true,
                                 exclusive: false,
                                 autoDelete: false,
                                 arguments: queueArguments!);

            if (_queueName == string.Empty)
            {
                _queueName = queuDeclareOk.QueueName;
                if (Logger.IsEnabled(LogLevel.Debug))
                {
                    Logger.LogDebug("RabbitMQ queue declared with name: {QueueName}", _queueName);
                }
            }

            // Declare retry exchange and queue
            if (_maxProcessRetries > 0)
            {
                await channel.ExchangeDeclareAsync(exchange: ExchangeRetry,
                    type: ExchangeType.Fanout);
                if (Logger.IsEnabled(LogLevel.Debug))
                {
                    Logger.LogDebug("Declaring RabbitMQ retry exchange: {ExchangeName} with type: {ExchangeType}", ExchangeRetry, ExchangeType.Fanout);
                }
                var argsRetry = new Dictionary<string, object>
                {
                    ["x-dead-letter-exchange"] = _exchange,
                    ["x-message-ttl"] = _processRetryDelayMs // default ttl in queue level
                };
                if (_queueType != null)
                {
                    argsRetry["x-queue-type"] = _queueType;
                }

                if (Logger.IsEnabled(LogLevel.Debug))
                {
                    Logger.LogDebug("Declaring RabbitMQ retry queue: {QueueName} with arguments: {Arguments}", ExchangeRetry, argsRetry);
                }

                await channel.QueueDeclareAsync(queue: ExchangeRetry,
                                 durable: true,
                                 exclusive: false,
                                 autoDelete: false,
                                 arguments: argsRetry);
                await channel.QueueBindAsync(queue: ExchangeRetry,
                              exchange: ExchangeRetry,
                              routingKey: string.Empty);
            }

            channel.CallbackExceptionAsync += async (sender, ea) =>
            {
                Logger.LogWarning(ea.Exception, "Recreating RabbitMQ consumer channel");

                _consumerChannel?.Dispose();
                _consumerChannel = await CreateConsumerChannelAsync();
                if (_consumerChannel != null)
                {
                    await StartBasicConsumeAsync();
                }
            };

            return channel;
        }

        private async Task<(bool Handled, bool Ok)> ProcessingEventAsync(BasicDeliverEventArgs eventArgs, string eventName, string message)
        {
            using var _ = Logger.BeginScope($"Processing integration event: {eventName}");
            if (await SubsManager.HasSubscriptionsForEventAsync(eventName))
            {
                using var scope = _scopeFactory.CreateScope();
                var subscriptions = await SubsManager.GetHandlersForEventAsync(eventName);
                if (Logger.IsEnabled(LogLevel.Trace))
                {
                    Logger.LogTrace("Found {count} handlers for event: {EventName}", subscriptions.Count(), eventName);
                }

                var eventType = await SubsManager.GetEventTypeByNameAsync(eventName);
                if (eventType == null)
                {
                    Logger.LogWarning("No event type found for event: {EventName}", eventName);
                    return (false, false);
                }
                var concreteType = typeof(IIntegrationEventHandler<>).MakeGenericType(eventType);

                var integrationEvent = JsonConvert.DeserializeObject(message, eventType);
                if (integrationEvent == null)
                {
                    Logger.LogWarning("Failed to deserialize message to {eventType}", eventType.Name);
                    return (false, false);
                }
                var tenantId =
                    eventArgs.BasicProperties.Headers?.ContainsKey("TenantId") == true &&
                    eventArgs.BasicProperties.Headers?["TenantId"] is byte[] tenant ? Encoding.UTF8.GetString(tenant) : null;
                var tenantResolver = scope.ServiceProvider.GetService<IScopedTenantResolver>();
                using var _1 = tenantResolver?.Resolve(tenantId);
                bool ok = false, handled = false;
                foreach (var subscription in subscriptions)
                {
                    if (!subscription.HandlerType.IsAssignableTo(concreteType))
                    {
                        Logger.LogWarning("Type {typeName} not assignable to {concreteType}", subscription.HandlerType.Name, concreteType.Name);

                        continue;
                    }
                    var handler = scope.ServiceProvider.GetService(subscription.HandlerType);
                    if (handler == null)
                    {
                        Logger.LogWarning("Type {typeName} not registered as a service", subscription.HandlerType.Name);

                        continue;
                    }

                    try
                    {
                        handled = true;
                        await (Task)concreteType.GetMethod(nameof(IIntegrationEventHandler<IntegrationEvent>.HandleAsync))!.Invoke(handler, new object[] { integrationEvent! })!;
                        ok = true;
                    }
                    catch (Exception ex)
                    {
                        var eventId = integrationEvent != null ? ((IntegrationEvent)integrationEvent).Id : Guid.Empty;
                        Logger.LogError(ex, "{handler} failed to handle event: {EventName}, eventId: {eventId}", handler.GetGenericTypeName(), eventName, eventId);
                        if (Logger.IsEnabled(LogLevel.Trace))
                        {
                            Logger.LogTrace(ex, "Event: {EventName}, eventId: {eventId} exception stack trace: {StackTrace}", eventName, eventId, ex.StackTrace);
                        }
                    }
                }
                return (handled, ok);
            }
            else
            {
                Logger.LogDebug("No subscription for RabbitMQ event: {EventName}", eventName);
                return (false, false);
            }
        }

        #endregion

        #region Subscribe/UnSubscribe
        public async ValueTask SubscribeAsync<T, TH>(string? key = default)
            where T : IntegrationEvent
            where TH : IIntegrationEventHandler<T>
        {
            await SubsManager.AddSubscriptionAsync<T, TH>(key);

            if (_consumerChannel == null)
            {
                _consumerChannel = await CreateConsumerChannelAsync();
                if (_consumerChannel == null) { throw new InvalidOperationException("RabbitMQ consumer channel cannot be initialized"); }
                await StartBasicConsumeAsync();
            }

            var eventName = key ?? SubsManager.GetDefaultEventKey<T>();
            await DoInternalSubscriptionAsync(eventName);
            Logger.LogInformation("Subscribing to event {EventName} with {EventHandler}", eventName, typeof(TH).GetGenericTypeName());

        }

        /// <summary>
        /// Use a new channel to unbind the queue to the exchange
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="eventName"></param>
        private async Task DoInternalUnsubscriptionAsync(object? sender, string eventName)
        {
            if (!_persistentConnection.IsConnected && !await _persistentConnection.TryConnectAsync())
            {
                return;
            }
            using var channel = await _persistentConnection.CreateChannelAsync();
            if (channel == null) { return; }
            await channel.QueueUnbindAsync(queue: _queueName,
                exchange: _exchange,
                routingKey: eventName);

            Logger.LogInformation("Queue unbind {queueName}", _queueName);

            if (SubsManager.IsEmpty && _consumerChannel != null)
            {
                await _consumerChannel.CloseAsync();
            }

        }

        /// <summary>
        /// Use a new channel to bind the queue to the exchange
        /// </summary>
        /// <param name="eventName"></param>
        /// <exception cref="InvalidOperationException"></exception>
        private async Task DoInternalSubscriptionAsync(string eventName)
        {
            if (!_persistentConnection.IsConnected && !await _persistentConnection.TryConnectAsync())
            {
                throw new InvalidOperationException("RabbitMQ broker is not connected");
            }
            using var channel = await _persistentConnection.CreateChannelAsync();
            if (channel == null) { throw new InvalidOperationException("RabbitMQ channel cannot be created"); }
            await channel.QueueBindAsync(queue: _queueName,
                             exchange: _exchange,
                             routingKey: eventName);
        }

        public virtual ValueTask UnsubscribeAsync<T, TH>(string? key = default)
            where T : IntegrationEvent
            where TH : IIntegrationEventHandler<T>
        {
            var eventName = SubsManager.GetDefaultEventKey<T>();

            Logger.LogInformation("Unsubscribing event {EventName} for hanler {Handler}", eventName, typeof(TH).GetGenericTypeName());

            SubsManager.RemoveSubscriptionAsync<T, TH>(key);

            return ValueTask.CompletedTask;
        }
        #endregion

        #region Init producer channel
        private async ValueTask<IChannel?> CreateProducerChannelAsync()
        {
            if (!_persistentConnection.IsConnected && !await _persistentConnection.TryConnectAsync())
            {
                return null;
            }
            Logger.LogInformation("Creating RabbitMQ producer channel. Broker: {Broker}.", _exchange);
            var channel = await _persistentConnection.CreateChannelAsync();
            if (channel == null) { return null; }
            await channel.ExchangeDeclareAsync(exchange: _exchange,
                                    type: _exchangeType);
            return channel;
        }
        #endregion

        #region Publish outgoing event
        public async ValueTask PublishAsync(IntegrationEvent @event, string? tenantId = default)
        {
            await Task.Yield();
            ArgumentNullException.ThrowIfNull(@event);
            if (!_persistentConnection.IsConnected && !await _persistentConnection.TryConnectAsync())
            {
                throw new InvalidOperationException("RabbitMQ broker is not connected");
            }
            if (tenantId != null && @event is IMultiTenantIntegrationEvent _t && _t.TenantId != null && !_t.TenantId.Equals(tenantId, StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException("TenantId is not match with event tenantId", nameof(tenantId));
            }
            tenantId ??= @event is IMultiTenantIntegrationEvent tenantEvent ? tenantEvent.TenantId : default;

            var policy = RetryPolicy.Handle<BrokerUnreachableException>()
                .Or<SocketException>()
                .WaitAndRetry(_retryCount, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)), (ex, time) =>
                {
                    Logger.LogWarning(ex, "Could not publish event: {EventId} after {Timeout}s ({ExceptionMessage})", @event.Id, $"{time.TotalSeconds:n1}", ex.Message);
                });

            var eventName = @event.GetEventKey();

            if (Logger.IsEnabled(LogLevel.Trace))
            {
                Logger.LogTrace("Declaring RabbitMQ exchange to publish event: {EventId}", @event.Id);
            }

            var body = System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(@event, @event.GetType(), new JsonSerializerOptions
            {
                WriteIndented = true
            });

            await policy.Execute(async () =>
            {
                if (_producerChannel == null)
                {
                    _producerChannel = await CreateProducerChannelAsync();
                    if (_producerChannel == null) { throw new InvalidOperationException("RabbitMQ producer channel cannot be initialized"); }
                }
                var properties = new BasicProperties
                {
                    ContentType = "application/json",
                    CorrelationId = @event.Id.ToString(),
                    MessageId = Guid.NewGuid().ToString(),
                    Timestamp = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds()),
                    DeliveryMode = DeliveryModes.Persistent,
                    Headers = new Dictionary<string, object?>()
                    {
                        { "TenantId", tenantId ?? string.Empty }
                    }
                };

                if (Logger.IsEnabled(LogLevel.Debug))
                {
                    Logger.LogDebug("Publishing event to RabbitMQ: {EventId} {EventName}", @event.Id, eventName);
                }

                await _producerChannel.BasicPublishAsync(
                    exchange: _exchange,
                    routingKey: eventName,
                    mandatory: true,
                    basicProperties: properties,
                    body: body);
            });
        }

        #endregion

        #region Dispose

        public async ValueTask CloseAsync()
        {
            if (_consumerChannel != null)
            {
                await _consumerChannel.CloseAsync();
            }
            if (_producerChannel != null)
            {
                await _producerChannel.CloseAsync();
            }
            if (_persistentConnection != null)
            {
                await _persistentConnection.DisconnectAsync();
            }
        }

        private bool _disposedValue;

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                if (disposing)
                {
                    // dispose managed state (managed objects)
                    _consumerChannel?.Dispose();
                    _consumerChannel = null!;
                    _producerChannel?.Dispose();
                    _producerChannel = null!;
                    _persistentConnection?.Dispose();
                    _persistentConnection = null!;
                    SubsManager?.Clear();
                    _scopeFactory = null!;
                }

                // free unmanaged resources (unmanaged objects) and override finalizer
                // set large fields to null

                _disposedValue = true;
            }
        }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
        #endregion
    }

    internal class RabbitMQEventBus<T> : RabbitMQEventBus, IEventBus<T>
    {
        public RabbitMQEventBus(IEventBusSubscriptionsManager subscriptionsManager,
            IServiceScopeFactory scopeFactory,
            ILogger logger,
            IRabbitMQPersistentConnection mQPersistentConnection,
            RabbitMQOptions options)
            : base(subscriptionsManager, scopeFactory, logger, mQPersistentConnection, options)
        {
        }
    }
}
