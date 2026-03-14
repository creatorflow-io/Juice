using System.Diagnostics;
using Juice.EventBus.Publishing;
using Juice.MediatR;
using Juice.Messaging.Integrations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Juice.Messaging.Local.Internal
{
    /// <summary>
    /// Scoped <see cref="ITransportPublisher"/> registered under the reserved key <c>"local"</c>.
    /// Called by the existing <c>DeliveryHostedService</c> when processing outbox deliveries
    /// with <c>PublisherKey = "local"</c>. Deserializes the payload and dispatches to in-process
    /// handlers using the same mechanism as the local-channel path.
    /// </summary>
    internal sealed class LocalTransportPublisher : ITransportPublisher
    {
        public string Key => "local";

        private readonly IMessageSerializer _serializer;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IntegrationEventDispatcher _dispatcher;
        private readonly IMediator _mediator;
        private readonly ILogger<LocalTransportPublisher> _logger;

        public LocalTransportPublisher(
            IMessageSerializer serializer,
            IServiceScopeFactory scopeFactory,
            IntegrationEventDispatcher dispatcher,
            IMediator mediator,
            ILogger<LocalTransportPublisher> logger)
        {
            _serializer = serializer;
            _scopeFactory = scopeFactory;
            _dispatcher = dispatcher;
            _mediator = mediator;
            _logger = logger;
        }

        private static string? TryGetHeader(IDictionary<string, object?>? headers, string key)
            => headers != null && headers.TryGetValue(key, out var value) ? value?.ToString() : null;

        public async ValueTask PublishAsync(
            byte[] payload,
            PublishContext context,
            CancellationToken cancellationToken = default)
        {
            var stopwatch = Stopwatch.StartNew();
            LocalChannelMetrics.IncrementDeliveryAttempt(Key);

            var message = _serializer.DeserializeFromUtf8Bytes<IMessage>(payload)
                ?? throw new InvalidOperationException(
                    $"Failed to deserialize message payload for delivery {context.MessageId}");

            // Ensure MessageContext is initialized from outbox headers so that
            // the idempotency key ("{Source}:{MessageId}") matches across
            // immediate dispatch and delivery retry paths.
            var needsContext = !MessageContext.IsInitialized;
            if (needsContext)
            {
                var headers = context.Headers;
                MessageContext.Initialize(
                    correlationId: TryGetHeader(headers, "x-correlation-id")
                        ?? Guid.NewGuid().ToString(),
                    causationId: TryGetHeader(headers, "x-causation-id"),
                    executionId: Guid.NewGuid().ToString(),
                    source: TryGetHeader(headers, "x-source") ?? Key);
            }

            try
            {
                if (message is INotification notification)
                {
                    await LocalDispatchHelper.DispatchNotificationAsync(_mediator, notification, cancellationToken);
                }
                else if (message is IIntegrationEvent integrationEvent)
                {
                    using var scope = _scopeFactory.CreateScope();
                    var result = await LocalDispatchHelper.DispatchIntegrationEventAsync(
                        scope.ServiceProvider, _dispatcher, integrationEvent, cancellationToken);
                    if (result == Integrations.EventDispatchResult.Failure)
                    {
                        throw new InvalidOperationException(
                            $"One or more handlers failed to process event {integrationEvent.GetType().Name} (MessageId={integrationEvent.MessageId})");
                    }
                }
                else
                {
                    _logger.LogWarning(
                        "Message type {MessageType} is neither INotification nor IIntegrationEvent — skipped",
                        message.GetType().Name);
                }

                stopwatch.Stop();
                LocalChannelMetrics.RecordDeliveryLatency(Key, stopwatch.Elapsed);
                LocalChannelMetrics.IncrementDeliverySuccess(Key);
            }
            catch
            {
                stopwatch.Stop();
                LocalChannelMetrics.RecordDeliveryLatency(Key, stopwatch.Elapsed);
                throw;
            }
            finally
            {
                if (needsContext)
                {
                    MessageContext.Clear();
                }
            }
        }
    }
}
