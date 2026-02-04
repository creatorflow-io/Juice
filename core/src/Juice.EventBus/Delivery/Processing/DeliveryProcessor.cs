using System.Diagnostics;
using Juice.EventBus.Delivery.Policies;
using Juice.EventBus.Publishing;
using Juice.EventBus.Registry;
using Microsoft.Extensions.Logging;

namespace Juice.EventBus.Delivery.Processing
{
    internal sealed class DeliveryProcessor<TContext>
    {
        private readonly IOutboxRepository _outbox;
        private readonly IEventTypeRegistry _typeRegistry;
        private readonly IEventSerializer _serializer;
        private readonly ILogger _logger;

        private IEventPublisher _publisher = default!;
        private DeliveryPolicy _deliveryPolicy = default!;

        public DeliveryProcessor(
            IOutboxRepository<TContext> outboxRepository,
            IEventTypeRegistry typeRegistry,
            IEventSerializer serializer,
            ILogger<DeliveryProcessor<TContext>> logger)
        {
            _outbox = outboxRepository;
            _typeRegistry = typeRegistry;
            _serializer = serializer;
            _logger = logger;
        }

        public void Configure(IEventPublisher publisher, DeliveryPolicy deliveryPolicy)
        {
            _publisher = publisher ?? throw new ArgumentNullException(nameof(publisher));
            _deliveryPolicy = deliveryPolicy ?? throw new ArgumentNullException(nameof(deliveryPolicy));
        }

        public async Task ProcessAsync(OutboxDelivery[] deliveries, CancellationToken cancellationToken = default)
        {
            EnsureConfigured();

            foreach (var delivery in deliveries)
            {
                // Before processing
                if (delivery.State == DeliveryState.Published)
                {
                    _logger.LogWarning("Delivery {DeliveryId} already published, skipping", delivery.DeliveryId);
                    continue;
                }
                await ProcessSingleDeliveryAsync(delivery, cancellationToken);
            }
        }

        private void EnsureConfigured()
        {
            if (_publisher is null || _deliveryPolicy is null)
            {
                throw new InvalidOperationException(
                    "DeliveryProcessor not configured. Call Configure() before ProcessAsync().");
            }
        }

        private async Task ProcessSingleDeliveryAsync(OutboxDelivery delivery, CancellationToken cancellationToken)
        {
            using var _ = _logger.BeginScope(new Dictionary<string, object?> { { "TraceId", delivery.DeliveryId } });
            var sw = Stopwatch.StartNew();
            try
            {
                DeliveryMetrics.IncrementDeliveryAttempt(delivery.PublisherKey);

                var afftected = await _outbox.MarkAsInProgressAsync(delivery.DeliveryId, cancellationToken);
                if (afftected == 0)
                {
                    _logger.LogInformation("Delivery {DeliveryId} is already being processed by another worker", delivery.DeliveryId);
                    return;
                }
                var evt = DeserializeEvent(delivery.OutboxEvent);
                if (evt == null)
                {
                    await MarkAsSkippedAsync(delivery, "Deserialization failed", cancellationToken);
                    return;
                }

                await PublishEventAsync(delivery, evt, cancellationToken);
                await _outbox.MarkAsPublishedAsync(delivery.DeliveryId, cancellationToken);

                DeliveryMetrics.IncrementDeliverySuccess(delivery.PublisherKey);

                if (_logger.IsEnabled(LogLevel.Debug))
                {
                    _logger.LogDebug("Successfully published delivery {DeliveryId}", delivery.DeliveryId);
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                _logger.LogInformation("Delivery {DeliveryId} cancelled", delivery.DeliveryId);
                throw;
            }
            catch (Exception ex)
            {
                await HandleDeliveryFailureAsync(delivery, ex, cancellationToken);
                DeliveryMetrics.IncrementDeliveryFailure(delivery.PublisherKey, ex.GetType().Name);
            }
            finally
            {
                sw.Stop();
                DeliveryMetrics.RecordDeliveryLatency(
                    delivery.PublisherKey,
                    sw.Elapsed);
            }
        }

        private IIntegrationEvent? DeserializeEvent(OutboxEvent outbox)
        {
            if(outbox == null)
            {
                _logger.LogWarning("Outbox event is null");
                return null;
            }
            var type = _typeRegistry.Resolve(outbox.EventTypeName);
            if (type == null)
            {
                _logger.LogWarning("Unknown event type: {EventType}", outbox.EventTypeName);
                return null;
            }

            try
            {
                return _serializer.Deserialize(outbox.Payload, type);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to deserialize event {EventId}", outbox.EventId);
                return null;
            }
        }

        private async Task PublishEventAsync(OutboxDelivery delivery, IIntegrationEvent evt, CancellationToken cancellationToken)
        {
            var context = new PublishContext
            {
                TenantId = delivery.OutboxEvent.TenantId,
                Destination = delivery.Destination
            };

            await _publisher.PublishAsync(evt, context, cancellationToken);
        }

        private async Task MarkAsSkippedAsync(OutboxDelivery delivery, string reason, CancellationToken cancellationToken)
        {
            _logger.LogWarning("Skipping delivery {DeliveryId}: {Reason}", delivery.DeliveryId, reason);
            await _outbox.MarkAsSkippedAsync(delivery.DeliveryId, reason, cancellationToken);
        }

        private async Task HandleDeliveryFailureAsync(OutboxDelivery delivery, Exception ex, CancellationToken cancellationToken)
        {
            _logger.LogError(ex, "Failed to publish delivery {DeliveryId}: {Message}",
                delivery.DeliveryId, ex.Message);

            if (_logger.IsEnabled(LogLevel.Trace))
            {
                _logger.LogTrace("Delivery {DeliveryId} exception details: {Exception}",
                    delivery.DeliveryId, ex.ToString());
            }

            var nextAttempt = _deliveryPolicy.GetNextAttempt(delivery.RetryCount + 1);
            await _outbox.MarkAsFailedAsync(delivery.DeliveryId, ex.Message, nextAttempt, cancellationToken);
        }
    }
}
