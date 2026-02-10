using System.Diagnostics;
using Juice.EventBus.Publishing;
using Juice.Messaging.Outbox.Delivery.Registry;
using Microsoft.Extensions.Logging;

namespace Juice.Messaging.Outbox.Delivery.Processing
{
    internal sealed class DeliveryProcessor<TContext>
    {
        private readonly IOutboxRepository _outbox;
        private readonly ILogger _logger;

        private ITransportPublisher _publisher = default!;
        private DeliveryPolicy _deliveryPolicy = default!;

        public DeliveryProcessor(
            IOutboxRepository<TContext> outboxRepository,
            ILogger<DeliveryProcessor<TContext>> logger)
        {
            _outbox = outboxRepository;
            _logger = logger;
        }

        public void Configure(ITransportPublisher publisher, DeliveryPolicy deliveryPolicy)
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
                await PublishEventAsync(delivery, cancellationToken);
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

        private async Task PublishEventAsync(OutboxDelivery delivery, CancellationToken cancellationToken)
        {
            var headers = delivery.OutboxEvent.Headers;
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug(
                    "Publishing outbox event {@log}",
                    headers);
            }
            var context = new PublishContext(delivery.OutboxEvent.EventId.ToString())
            {
                TenantId = delivery.OutboxEvent.TenantId,
                Destination = delivery.Destination,
                Headers = headers
            };

            await _publisher.PublishAsync(delivery.OutboxEvent.PayloadBytes, context, cancellationToken);
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
