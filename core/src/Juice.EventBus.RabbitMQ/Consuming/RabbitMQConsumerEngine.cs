using System.Text;
using Juice.EventBus.Dispatching;
using Juice.EventBus.RabbitMQ.Policies;
using Juice.EventBus.Subscriptions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Juice.EventBus.RabbitMQ.Consuming
{
    internal sealed class RabbitMQConsumerEngine
    {
        private IRabbitMQPersistentConnection _persistentConnection = default!;

        private IChannel? _consumerChannel;

        private string _queueName = default!;
        private ushort _qosPrefetchCount = 1;

        private readonly IEventBusSubscriptionsManager _subscriptionsManager;
        private readonly IntegrationEventDispatcher _dispatcher;
        private readonly IRetryPolicyProvider? _retryPolicyProvider;
        private readonly ILogger _logger;
        private readonly IServiceProvider _keyedService;
        private DeadLetterConfig? _deadLetterConfig;

        public RabbitMQConsumerEngine(
            IServiceProvider keyedService,
            IEventBusSubscriptionsManager subscriptionsManager,
            IntegrationEventDispatcher dispatcher,
            ILogger<RabbitMQConsumerEngine> logger,
            IRetryPolicyProvider? retryPolicyProvider = default
            )
        {
            _retryPolicyProvider = retryPolicyProvider;
            _dispatcher = dispatcher;
            _subscriptionsManager = subscriptionsManager;
            _logger = logger;
            _keyedService = keyedService;
        }

        public async Task<bool> StartAsync(RabbitMQConsumerEndpoint endpoint, CancellationToken cancellationToken)
        {
            _persistentConnection = _keyedService.GetKeyedService<IRabbitMQPersistentConnection>(endpoint.ConnectionName)
                ?? throw new InvalidOperationException($"RabbitMQ connection with name '{endpoint.ConnectionName}' is not registered.");
            _queueName = endpoint.Queue;

            _qosPrefetchCount = endpoint.QosPrefetchCount;

            _deadLetterConfig = endpoint.DeadLetter;

            _consumerChannel = await CreateConsumerChannelAsync(cancellationToken);
            if (_consumerChannel == null)
            {
                _logger.LogError("[{Queue}]Failed to create RabbitMQ consumer channel.", _queueName);
                return false;
            }
            await StartBasicConsumeAsync();
            return true;
        }

        public async Task StopAsync()
        {
            try
            {
                if (_consumerChannel != null)
                {
                    await _consumerChannel.CloseAsync();
                    _consumerChannel.Dispose();
                }
                _persistentConnection?.Dispose();
                _persistentConnection = null!;
            }
            catch { }
        }

        #region Init consume channel and processing incoming event

        private async Task Consumer_ReceivedAsync(object sender, BasicDeliverEventArgs eventArgs)
        {
            var headers = eventArgs.BasicProperties.Headers ?? new Dictionary<string, object?>();
            var eventName = headers.GetHeaderString("x-original-routing-key") ?? eventArgs.RoutingKey;
            var message = Encoding.UTF8.GetString(eventArgs.Body.Span);

            try
            {
                if (_logger.IsEnabled(LogLevel.Debug))
                {
                    _logger.LogDebug("[{Queue}] Received event: {EventName} from {Broker}", _queueName, eventName, eventArgs.Exchange);
                }
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
                    // in a REAL WORLD app this should be handled with a Dead Letter _defaultExchange (DLX). 
                    // For more information see: https://www.rabbitmq.com/dlx.html
                    await _consumerChannel.BasicAckAsync(eventArgs.DeliveryTag, multiple: false);
                    return;
                }
                if (!processed)
                {
                    // No handler found for event or processing failed and no retries are allowed
                    if (_logger.IsEnabled(LogLevel.Debug))
                    {
                        _logger.LogDebug("No handler processed RabbitMQ event: {EventName}", eventName);
                    }
                    if (_deadLetterConfig?.Enabled == true)
                    {
                        await SendToDeadLetterQueueAsync(eventArgs, "NoHandlerFoundOrProcessingFailed");
                    }
                    else
                    {
                        await _consumerChannel.BasicNackAsync(eventArgs.DeliveryTag, multiple: false, requeue: false);
                    }
                    return;
                }
                #region Retry processing
                var originalExchange = headers.GetHeaderString("x-original-exchange") ?? eventArgs.Exchange;
                var attempts = (headers.GetHeaderInt("x-attempts") ?? 0) + 1;

                var retryPolicy = _retryPolicyProvider == null ? null
                    : await _retryPolicyProvider.GetRetryPolicyForSourceAsync(originalExchange, attempts);

                if (retryPolicy == null || retryPolicy.IsMaxRetryReached)
                {
                    // Route to DLQ after max retries
                    if (_deadLetterConfig?.Enabled == true)
                    {
                        if (_logger.IsEnabled(LogLevel.Debug))
                        {
                            _logger.LogDebug("Max processing retries reached for event: {EventName}, attempts: {Attempts}. Send to DLX.", eventName, attempts);
                        }
                        await SendToDeadLetterQueueAsync(eventArgs,
                            retryPolicy?.IsMaxRetryReached == true
                                ? $"MaxRetriesReached_Attempts_{attempts}"
                                : "NoRetryPolicyDefined");
                    }
                    else
                    {
                        if (_logger.IsEnabled(LogLevel.Debug))
                        {
                            _logger.LogDebug("Max processing retries reached for event: {EventName}, attempts: {Attempts}. Send Nack.", eventName, attempts);
                        }
                        await _consumerChannel.BasicNackAsync(eventArgs.DeliveryTag, multiple: false, requeue: false);
                    }
                    return;
                }

                await RetryAsync(eventArgs, originalExchange, eventName, retryPolicy, attempts);
                #endregion
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "----- ERROR Processing message \"{0}\". {1}", message, ex.Message);
                if (_logger.IsEnabled(LogLevel.Trace))
                {
                    _logger.LogTrace(ex.StackTrace);
                }
            }

        }

        private async Task StartBasicConsumeAsync()
        {
            _logger.LogInformation("Starting RabbitMQ basic consume queue {queueName}.", _queueName);

            if (_consumerChannel != null)
            {
                var consumer = new AsyncEventingBasicConsumer(_consumerChannel);

                consumer.ReceivedAsync += Consumer_ReceivedAsync;

                await _consumerChannel.BasicConsumeAsync(
                    queue: _queueName,
                    autoAck: false,
                    consumer: consumer);

                await _consumerChannel.BasicQosAsync(0, _qosPrefetchCount, false);
            }
            else
            {
                _logger.LogError("StartBasicConsume can't call on _consumerChannel == null");
            }
        }

        /// <summary>
        /// Init exchanges and queues
        /// </summary>
        /// <returns></returns>
        private async ValueTask<IChannel?> CreateConsumerChannelAsync(CancellationToken cancellationToken)
        {
            if (!_persistentConnection.IsConnected && !await _persistentConnection.TryConnectAsync(cancellationToken).ConfigureAwait(false))
            {
                return null;
            }

            _logger.LogInformation("Creating RabbitMQ consumer channel.");

            var channel = await _persistentConnection.CreateChannelAsync(cancellationToken);
            if (channel == null) { return null; }

            channel.CallbackExceptionAsync += async (sender, ea) =>
            {
                _logger.LogWarning(ea.Exception, "Recreating RabbitMQ consumer channel");

                _consumerChannel?.Dispose();
                _consumerChannel = await CreateConsumerChannelAsync(default);
                if (_consumerChannel != null)
                {
                    await StartBasicConsumeAsync();
                }
            };

            return channel;
        }

        #endregion

        #region Processing

        private async Task<(bool Handled, bool Ok)> ProcessingEventAsync(BasicDeliverEventArgs eventArgs,
            string eventName, string message)
        {
            using var _ = _logger.BeginScope($"Processing integration event: {eventName}");
            if (await _subscriptionsManager.HasSubscriptionsForEventAsync(eventName))
            {
                var eventType = await _subscriptionsManager.GetEventTypeByNameAsync(eventName);
                if (eventType == null)
                {
                    _logger.LogWarning("No event type found for event: {EventName}", eventName);
                    return (false, false);
                }

                if (JsonConvert.DeserializeObject(message, eventType) is not IIntegrationEvent integrationEvent)
                {
                    _logger.LogWarning("Failed to deserialize message to {eventType}", eventType.Name);
                    return (false, false);
                }

                var tenantId = eventArgs.BasicProperties.Headers?.GetHeaderString("x-tenant-id");

                return await _dispatcher.DispatchAsync(integrationEvent, new EventDispatchContext
                {
                    EventName = eventName,
                    TenantId = tenantId
                }, CancellationToken.None);
            }
            else
            {
                _logger.LogDebug("No subscription for RabbitMQ event: {EventName}", eventName);
                return (false, false);
            }
        }

        private async Task RetryAsync(BasicDeliverEventArgs eventArgs,
            string originalExchange, string originalRoutingKey,
            RetryPolicy retryPolicy, int attempts)
        {
            if (_consumerChannel == null)
            {
                return;
            }
            var headers = eventArgs.BasicProperties.Headers ?? new Dictionary<string, object?>();

            headers["x-attempts"] = attempts;
            headers["x-original-exchange"] = originalExchange;
            headers["x-original-routing-key"] = originalRoutingKey;

            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug("Retrying event: {EventName}, attempts: {Attempts}, exchange: {Exchange}, route: {Route}",
                    originalRoutingKey, headers["x-attempts"], retryPolicy.Exchange, retryPolicy.RoutingKey);
            }
            // re-publish with updated header
            await _consumerChannel.BasicPublishAsync(
                exchange: retryPolicy.Exchange,
                routingKey: retryPolicy.RoutingKey,
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

        private async Task SendToDeadLetterQueueAsync(BasicDeliverEventArgs eventArgs, string reason)
        {
            if (_consumerChannel == null || _deadLetterConfig == null)
            {
                return;
            }

            var headers = eventArgs.BasicProperties.Headers ?? new Dictionary<string, object?>();
            headers["x-death-reason"] = reason;
            headers["x-death-timestamp"] = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            headers["x-original-queue"] = _queueName;

            var originalRoutingKey = headers.GetHeaderString("x-original-routing-key") ?? eventArgs.RoutingKey;
            var routingKey = _deadLetterConfig.GetRoutingKey(originalRoutingKey);
            _logger.LogWarning(
                "Sending message to DLQ. Queue: {Queue}, Original: {Event}, Routing: {Routing}, Reason: {Reason}",
                _queueName,
                originalRoutingKey, routingKey,
                reason);

            await _consumerChannel.BasicPublishAsync(
                exchange: _deadLetterConfig.Exchange,
                routingKey: _deadLetterConfig.GetRoutingKey(originalRoutingKey),
                mandatory: false,
                basicProperties: new BasicProperties
                {
                    ContentType = eventArgs.BasicProperties.ContentType,
                    CorrelationId = eventArgs.BasicProperties.CorrelationId,
                    MessageId = eventArgs.BasicProperties.MessageId,
                    Timestamp = eventArgs.BasicProperties.Timestamp,
                    DeliveryMode = DeliveryModes.Persistent,
                    Headers = headers
                },
                body: eventArgs.Body);

            await _consumerChannel.BasicAckAsync(eventArgs.DeliveryTag, multiple: false);
        }
        #endregion
    }
}
