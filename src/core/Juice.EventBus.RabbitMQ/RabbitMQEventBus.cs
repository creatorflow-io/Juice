using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;
using Polly.Retry;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.Exceptions;

namespace Juice.EventBus.RabbitMQ
{
    public class RabbitMQEventBus : EventBusBase, IDisposable
    {
        public const string BROKER_NAME = "juice_event_bus";

        private readonly IRabbitMQPersistentConnection _persistentConnection;

        private IModel _consumerChannel;
        private string _queueName;
        private readonly int _retryCount;

        private readonly IServiceScopeFactory _scopeFactory;


        public RabbitMQEventBus(IEventBusSubscriptionsManager subscriptionsManager,
            IServiceScopeFactory scopeFactory,
            ILogger<RabbitMQEventBus> logger,
            IRabbitMQPersistentConnection mQPersistentConnection,
            IOptionsSnapshot<RabbitMQOptions> options
            )
            : base(subscriptionsManager, logger)
        {
            _persistentConnection = mQPersistentConnection;
            _queueName = options.Value.SubscriptionClientName;
            _consumerChannel = CreateConsumerChannel();
            _scopeFactory = scopeFactory;
            _retryCount = options.Value.RetryCount;
            SubsManager.OnEventRemoved += SubsManager_OnEventRemoved;
        }

        #region Init consume channel and processing incoming event
        private void SubsManager_OnEventRemoved(object sender, string eventName)
        {
            if (!_persistentConnection.IsConnected)
            {
                _persistentConnection.TryConnect();
            }

            using (var channel = _persistentConnection.CreateModel())
            {
                channel.QueueUnbind(queue: _queueName,
                    exchange: BROKER_NAME,
                    routingKey: eventName);

                Logger.LogInformation("Queue unbind {queueName}", _queueName);

                if (SubsManager.IsEmpty)
                {
                    _queueName = string.Empty;
                    _consumerChannel.Close();
                }
            }

        }

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

                await ProcessingEventAsync(eventName, message);
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "----- ERROR Processing message \"{0}\". {1}", message, ex.Message);
                Logger.LogTrace(ex.StackTrace);
            }

            // Even on exception we take the message off the queue.
            // in a REAL WORLD app this should be handled with a Dead Letter Exchange (DLX). 
            // For more information see: https://www.rabbitmq.com/dlx.html
            _consumerChannel.BasicAck(eventArgs.DeliveryTag, multiple: false);
        }

        private void StartBasicConsume()
        {
            Logger.LogInformation("Starting RabbitMQ basic consume");

            if (_consumerChannel != null)
            {
                var consumer = new AsyncEventingBasicConsumer(_consumerChannel);

                consumer.Received += Consumer_ReceivedAsync;

                _consumerChannel.BasicConsume(
                    queue: _queueName,
                    autoAck: false,
                    consumer: consumer);
            }
            else
            {
                Logger.LogError("StartBasicConsume can't call on _consumerChannel == null");
            }
        }

        private IModel CreateConsumerChannel()
        {
            if (!_persistentConnection.IsConnected)
            {
                _persistentConnection.TryConnect();
            }

            Logger.LogInformation("Creating RabbitMQ consumer channel");

            var channel = _persistentConnection.CreateModel();

            channel.ExchangeDeclare(exchange: BROKER_NAME,
                                    type: "direct");

            channel.QueueDeclare(queue: _queueName,
                                 durable: true,
                                 exclusive: false,
                                 autoDelete: false,
                                 arguments: null);

            channel.CallbackException += (sender, ea) =>
            {
                Logger.LogWarning(ea.Exception, "Recreating RabbitMQ consumer channel");

                _consumerChannel.Dispose();
                _consumerChannel = CreateConsumerChannel();
                StartBasicConsume();
            };

            return channel;
        }

        private async Task ProcessingEventAsync(string eventName, string message)
        {
            Logger.LogDebug("Processing RabbitMQ event: {EventName}", eventName);

            if (SubsManager.HasSubscriptionsForEvent(eventName))
            {
                using var scope = _scopeFactory.CreateScope();
                var subscriptions = SubsManager.GetHandlersForEvent(eventName);
                Logger.LogTrace("Found {count} handlers for event: {EventName}", subscriptions.Count(), eventName);
                foreach (var subscription in subscriptions)
                {
                    var handler = scope.ServiceProvider.GetService(subscription.HandlerType);
                    if (handler == null)
                    {
                        Logger.LogWarning("Type {typeName} not registered as a service", subscription.HandlerType.Name);

                        continue;
                    }
                    var eventType = SubsManager.GetEventTypeByName(eventName);
                    var integrationEvent = JsonSerializer.Deserialize(message, eventType, new JsonSerializerOptions() { PropertyNameCaseInsensitive = true });
                    var concreteType = typeof(IIntegrationEventHandler<>).MakeGenericType(eventType);

                    await Task.Yield();
                    try
                    {
                        await (Task)concreteType.GetMethod(nameof(IIntegrationEventHandler<IntegrationEvent>.HandleAsync)).Invoke(handler, new object[] { integrationEvent });
                    }
                    catch (Exception ex)
                    {
                        var eventId = integrationEvent != null ? ((IntegrationEvent)integrationEvent).Id : Guid.Empty;
                        Logger.LogError(ex, "{handler} failed to handle event: {EventName}, eventId: {eventId}", handler.GetGenericTypeName(), eventName, eventId);
                    }
                }
            }
            else
            {
                Logger.LogDebug("No subscription for RabbitMQ event: {EventName}", eventName);
            }
        }

        #endregion

        #region Subscribe/UnSubscribe
        public override void Subscribe<T, TH>()
        {
            var eventName = SubsManager.GetEventKey<T>();

            DoInternalSubscription(eventName);

            Logger.LogInformation("Subscribing to event {EventName} with {EventHandler}", eventName, typeof(TH).GetGenericTypeName());

            SubsManager.AddSubscription<T, TH>();

            StartBasicConsume();

        }

        private void DoInternalSubscription(string eventName)
        {
            var containsKey = SubsManager.HasSubscriptionsForEvent(eventName);
            if (!containsKey)
            {
                if (!_persistentConnection.IsConnected)
                {
                    _persistentConnection.TryConnect();
                }

                using var channel = _persistentConnection.CreateModel();
                channel.QueueBind(queue: _queueName,
                                  exchange: BROKER_NAME,
                                  routingKey: eventName);
            }

        }
        #endregion

        #region Publish outgoing event
        public override async Task PublishAsync(IntegrationEvent @event)
        {
            if (!_persistentConnection.IsConnected)
            {
                _persistentConnection.TryConnect();
            }

            var policy = RetryPolicy.Handle<BrokerUnreachableException>()
                .Or<SocketException>()
                .WaitAndRetry(_retryCount, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)), (ex, time) =>
                {
                    Logger.LogWarning(ex, "Could not publish event: {EventId} after {Timeout}s ({ExceptionMessage})", @event.Id, $"{time.TotalSeconds:n1}", ex.Message);
                });

            var eventName = @event.GetType().Name;

            Logger.LogTrace("Creating RabbitMQ channel to publish event: {EventId} ({EventName})", @event.Id, eventName);

            using (var channel = _persistentConnection.CreateModel())
            {
                Logger.LogTrace("Declaring RabbitMQ exchange to publish event: {EventId}", @event.Id);

                channel.ExchangeDeclare(exchange: BROKER_NAME, type: "direct");

                var body = JsonSerializer.SerializeToUtf8Bytes(@event, @event.GetType(), new JsonSerializerOptions
                {
                    WriteIndented = true
                });

                policy.Execute(() =>
                {
                    var properties = channel.CreateBasicProperties();
                    properties.DeliveryMode = 2; // persistent

                    Logger.LogDebug("Publishing event to RabbitMQ: {EventId}", @event.Id);

                    channel.BasicPublish(
                        exchange: BROKER_NAME,
                        routingKey: eventName,
                        mandatory: true,
                        basicProperties: properties,
                        body: body);
                });
            }
        }
        #endregion

        public void Dispose()
        {
            if (_consumerChannel != null)
            {
                _consumerChannel.Dispose();
            }

            SubsManager.Clear();
        }
    }
}
