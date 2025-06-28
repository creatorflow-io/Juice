using System.Net.Sockets;
using System.Text;
using System.Text.Json;
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

        private IModel? _consumerChannel;
        private string _queueName;
        private string? _queueType; // classic (default), quorum

        private string _exchange = "default_exchange";
        private string ExchangeRetry => $"{_exchange}.retry";
        private string _exchangeType; // direct, fanout, topic, headers

        private IModel? _producerChannel;
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
            SubsManager.OnEventRemoved += DoInternalUnsubscription;
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
                    _consumerChannel.BasicAck(eventArgs.DeliveryTag, multiple: false);
                }
                else if (!processed || _maxProcessRetries <= 0) // if no handler found for event or processing failed and no retries are allowed
                {
                    Logger.LogWarning("No handler found for RabbitMQ event: {EventName}", eventName);
                    _consumerChannel.BasicNack(eventArgs.DeliveryTag, multiple: false, requeue: false);
                }
                else // if processing failed
                {
                    var headers = eventArgs.BasicProperties.Headers;
                    int attempts = headers.ContainsKey("x-attempts") ? int.Parse(headers["x-attempts"]?.ToString() ?? "0") : 0;
                    if (attempts >= _maxProcessRetries)
                    {
                        if (Logger.IsEnabled(LogLevel.Debug))
                        {
                            Logger.LogDebug("Max processing retries reached for event: {EventName}, attempts: {Attempts}", eventName, attempts);
                        }
                        _consumerChannel.BasicNack(eventArgs.DeliveryTag, multiple: true, requeue: false);
                    }
                    else
                    {
                        eventArgs.BasicProperties.Headers["x-attempts"] = attempts + 1;
                        if (Logger.IsEnabled(LogLevel.Debug))
                        {
                            Logger.LogDebug("Retrying event: {EventName}, attempts: {Attempts}", eventName, eventArgs.BasicProperties.Headers["x-attempts"]);
                        }
                        // re-publish with updated header
                        _consumerChannel.BasicPublish(ExchangeRetry, eventArgs.RoutingKey, eventArgs.BasicProperties, eventArgs.Body);
                        _consumerChannel.BasicAck(eventArgs.DeliveryTag, multiple: false);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "----- ERROR Processing message \"{0}\". {1}", message, ex.Message);
                Logger.LogTrace(ex.StackTrace);
            }

        }

        private void StartBasicConsume()
        {
            Logger.LogInformation("Starting RabbitMQ basic consume queue {queueName}.", _queueName);

            if (_consumerChannel != null)
            {
                var consumer = new AsyncEventingBasicConsumer(_consumerChannel);

                consumer.Received += Consumer_ReceivedAsync;

                _consumerChannel.BasicConsume(
                    queue: _queueName,
                    autoAck: false,
                    consumer: consumer);

                _consumerChannel.BasicQos(0, 1, false);
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
        private IModel? CreateConsumerChannel()
        {
            if (!_persistentConnection.IsConnected && !_persistentConnection.TryConnect())
            {
                return null;
            }

            Logger.LogInformation("Creating RabbitMQ consumer channel. Broker: {Broker}.", _exchange);

            var channel = _persistentConnection.CreateModel();
            if (channel == null) { return null; }

            channel.ExchangeDeclare(exchange: _exchange,
                                    type: _exchangeType);

            if (Logger.IsEnabled(LogLevel.Debug))
            {
                Logger.LogDebug("Declaring RabbitMQ exchange: {ExchangeName} with type: {ExchangeType}", _exchange, _exchangeType);
            }

            QueueDeclareOk queuDeclareOk = channel.QueueDeclare(queue: _queueName,
                                 durable: true,
                                 exclusive: false,
                                 autoDelete: false,
                                 arguments: _queueType != null
                                 ? new Dictionary<string, object> { { "x-queue-type", _queueType } }
                                 : null);

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
                channel.ExchangeDeclare(exchange: ExchangeRetry,
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

                channel.QueueDeclare(queue: ExchangeRetry,
                                 durable: true,
                                 exclusive: false,
                                 autoDelete: false,
                                 arguments: argsRetry);
                channel.QueueBind(queue: ExchangeRetry,
                              exchange: ExchangeRetry,
                              routingKey: string.Empty);
            }

            channel.CallbackException += (sender, ea) =>
            {
                Logger.LogWarning(ea.Exception, "Recreating RabbitMQ consumer channel");

                _consumerChannel?.Dispose();
                _consumerChannel = CreateConsumerChannel();
                if (_consumerChannel != null)
                {
                    StartBasicConsume();
                }
            };

            return channel;
        }

        private async Task<(bool Handled, bool Ok)> ProcessingEventAsync(BasicDeliverEventArgs eventArgs, string eventName, string message)
        {
            using (Logger.BeginScope($"Processing integration event: {eventName}"))
            {
                if (SubsManager.HasSubscriptionsForEvent(eventName))
                {
                    using var scope = _scopeFactory.CreateScope();
                    var subscriptions = SubsManager.GetHandlersForEvent(eventName);
                    if (Logger.IsEnabled(LogLevel.Trace))
                    {
                        Logger.LogTrace("Found {count} handlers for event: {EventName}", subscriptions.Count(), eventName);
                    }

                    var eventType = SubsManager.GetEventTypeByName(eventName);
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
                    using var _ = tenantResolver?.Resolve(tenantId);
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
        }

        #endregion

        #region Subscribe/UnSubscribe
        public void Subscribe<T, TH>(string? key = default)
            where T : IntegrationEvent
            where TH : IIntegrationEventHandler<T>
        {
            SubsManager.AddSubscription<T, TH>(key);

            if (_consumerChannel == null)
            {
                _consumerChannel = CreateConsumerChannel();
                if (_consumerChannel == null) { throw new InvalidOperationException("RabbitMQ consumer channel cannot be initialized"); }
                StartBasicConsume();
            }

            var eventName = key ?? SubsManager.GetDefaultEventKey<T>();
            DoInternalSubscription(eventName);
            Logger.LogInformation("Subscribing to event {EventName} with {EventHandler}", eventName, typeof(TH).GetGenericTypeName());

        }

        /// <summary>
        /// Use a new channel to unbind the queue to the exchange
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="eventName"></param>
        private void DoInternalUnsubscription(object? sender, string eventName)
        {
            if (!_persistentConnection.IsConnected && !_persistentConnection.TryConnect())
            {
                return;
            }
            using var channel = _persistentConnection.CreateModel();
            channel.QueueUnbind(queue: _queueName,
                exchange: _exchange,
                routingKey: eventName);

            Logger.LogInformation("Queue unbind {queueName}", _queueName);

            if (SubsManager.IsEmpty)
            {
                _consumerChannel?.Close();
            }

        }

        /// <summary>
        /// Use a new channel to bind the queue to the exchange
        /// </summary>
        /// <param name="eventName"></param>
        /// <exception cref="InvalidOperationException"></exception>
        private void DoInternalSubscription(string eventName)
        {
            if (!_persistentConnection.IsConnected && !_persistentConnection.TryConnect())
            {
                throw new InvalidOperationException("RabbitMQ broker is not connected");
            }
            using var channel = _persistentConnection.CreateModel();
            channel.QueueBind(queue: _queueName,
                              exchange: _exchange,
                              routingKey: eventName);
        }

        public virtual void Unsubscribe<T, TH>(string? key = default)
            where T : IntegrationEvent
            where TH : IIntegrationEventHandler<T>
        {
            var eventName = SubsManager.GetDefaultEventKey<T>();

            Logger.LogInformation("Unsubscribing event {EventName} for hanler {Handler}", eventName, typeof(TH).GetGenericTypeName());

            SubsManager.RemoveSubscription<T, TH>(key);

        }
        #endregion

        #region Init producer channel
        private IModel? CreateProducerChannel()
        {
            if (!_persistentConnection.IsConnected && !_persistentConnection.TryConnect())
            {
                return null;
            }
            Logger.LogInformation("Creating RabbitMQ producer channel. Broker: {Broker}.", _exchange);
            var channel = _persistentConnection.CreateModel();
            if (channel == null) { return null; }
            channel.ExchangeDeclare(exchange: _exchange,
                                    type: _exchangeType);
            return channel;
        }
        #endregion

        #region Publish outgoing event
        public async Task PublishAsync(IntegrationEvent @event, string? tenantId = default)
        {
            await Task.Yield();
            ArgumentNullException.ThrowIfNull(@event);
            if (!_persistentConnection.IsConnected && !_persistentConnection.TryConnect())
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

            policy.Execute(() =>
            {
                if (_producerChannel == null)
                {
                    _producerChannel = CreateProducerChannel();
                    if (_producerChannel == null) { throw new InvalidOperationException("RabbitMQ producer channel cannot be initialized"); }
                }
                var properties = _producerChannel.CreateBasicProperties();
                properties.DeliveryMode = 2; // persistent
                properties.Headers = new Dictionary<string, object>
                {
                    { "TenantId", tenantId ?? string.Empty }
                };

                if (Logger.IsEnabled(LogLevel.Debug))
                {
                    Logger.LogDebug("Publishing event to RabbitMQ: {EventId} {EventName}", @event.Id, eventName);
                }

                _producerChannel.BasicPublish(
                    exchange: _exchange,
                    routingKey: eventName,
                    mandatory: true,
                    basicProperties: properties,
                    body: body);
            });
        }

        #endregion

        #region Dispose
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
