using System;
using System.Diagnostics;
using System.Text;
using Juice.EventBus.Publishing;
using Juice.Messaging.Diagnostics;
using Juice.Messaging.Publishing;
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

        /// <summary>
        /// Processes a single outbox delivery through the full InProgress → Published/Failed cycle.
        /// <para>
        /// For the <c>"local"</c> publisher: if phase 1 (immediate dispatch via
        /// <c>LocalChannelBackgroundService</c>) succeeded and marked the record <c>Published</c>,
        /// <c>SendPendingIntent</c> never retrieves it (it filters to <c>NotPublished</c> only),
        /// so this method is not called for those records. This path handles phase 1 failures,
        /// crash-recovery scenarios, and any case where the outbox record was not closed by phase 1.
        /// </para>
        /// </summary>
        private async Task ProcessSingleDeliveryAsync(OutboxDelivery delivery, CancellationToken cancellationToken)
        {
            // "TraceId" is the job key of the Juice.Extensions.Logging providers (one file / log group per delivery),
            // not the OpenTelemetry trace id
            using var _ = _logger.BeginScope(new Dictionary<string, object?> { { "TraceId", delivery.DeliveryId } });
            using var activity = MessagingActivity.StartDelivery(delivery.PublisherKey,
                delivery.OutboxEvent?.EventTypeName, delivery.EventId.ToString(), delivery.OutboxEvent?.Headers);
            var sw = Stopwatch.StartNew();
            // RetryCount as stored once this worker has claimed the delivery
            var retryCount = delivery.RetryCount;
            try
            {
                DeliveryMetrics.IncrementDeliveryAttempt(delivery.PublisherKey);

                // InProgress here means recover-timeout picked up a delivery whose worker never finished it
                var reclaim = delivery.State == DeliveryState.InProgress;
                var afftected = reclaim
                    ? await _outbox.ReclaimInProgressAsync(delivery.DeliveryId, delivery.ProcessedOn, cancellationToken)
                    : await _outbox.MarkAsInProgressAsync(delivery.DeliveryId, cancellationToken);
                if (afftected == 0)
                {
                    _logger.LogInformation("Delivery {DeliveryId} is already being processed by another worker", delivery.DeliveryId);
                    activity?.SetTag(MessagingActivity.Tags.DeliveryOutcome, "claimed_elsewhere");
                    return;
                }
                if (delivery.State != DeliveryState.NotPublished)
                {
                    retryCount++;
                }
                if (reclaim && retryCount > _deliveryPolicy.MaxRetryAttempts)
                {
                    // a delivery that hangs or crashes its worker must not be reclaimed forever
                    _logger.LogError("Delivery {DeliveryId} did not finish after {RetryCount} attempts; giving up", delivery.DeliveryId, retryCount);
                    await _outbox.MarkAsFailedAsync(delivery.DeliveryId,
                        $"Abandoned: the delivery did not finish within {_deliveryPolicy.Timeout} after {retryCount} attempts", null, cancellationToken);
                    DeliveryMetrics.IncrementDeliveryFailure(delivery.PublisherKey, "Abandoned");
                    activity?.SetTag(MessagingActivity.Tags.DeliveryOutcome, "abandoned");
                    activity?.SetStatus(ActivityStatusCode.Error, "Abandoned");
                    MessagingMetrics.RecordOutboxError(delivery.PublisherKey, delivery.OutboxEvent?.EventTypeName, "Abandoned");
                    return;
                }
                activity?.SetTag(MessagingActivity.Tags.RetryCount, retryCount);
                await PublishEventAsync(delivery, cancellationToken);
                await _outbox.MarkAsPublishedAsync(delivery.DeliveryId, cancellationToken);

                DeliveryMetrics.IncrementDeliverySuccess(delivery.PublisherKey);
                activity?.SetTag(MessagingActivity.Tags.DeliveryOutcome, "published");
                MessagingMetrics.RecordOutboxPublished(delivery.PublisherKey, delivery.OutboxEvent?.EventTypeName);

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
                activity?.SetTag(MessagingActivity.Tags.DeliveryOutcome, "failed");
                MessagingActivity.SetError(activity, ex);
                MessagingMetrics.RecordOutboxError(delivery.PublisherKey, delivery.OutboxEvent?.EventTypeName, ex.GetType().FullName ?? ex.GetType().Name);
                await HandleDeliveryFailureAsync(delivery, retryCount, ex, cancellationToken);
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
                    "Publishing outbox event {@log}. Payload: {Payload}",
                    headers, Encoding.UTF8.GetString(delivery.OutboxEvent.PayloadBytes));
            }
            var context = new PublishContext(delivery.OutboxEvent.EventId.ToString())
            {
                TenantId = delivery.OutboxEvent.TenantId,
                Destination = delivery.Destination,
                Headers = headers,
                RoutingKey = delivery.RoutingKey
            };

            await _publisher.PublishAsync(delivery.OutboxEvent.PayloadBytes, context, cancellationToken);
        }

        private async Task MarkAsSkippedAsync(OutboxDelivery delivery, string reason, CancellationToken cancellationToken)
        {
            _logger.LogWarning("Skipping delivery {DeliveryId}: {Reason}", delivery.DeliveryId, reason);
            await _outbox.MarkAsSkippedAsync(delivery.DeliveryId, reason, cancellationToken);
        }

        private async Task HandleDeliveryFailureAsync(OutboxDelivery delivery, int retryCount, Exception ex, CancellationToken cancellationToken)
        {
            _logger.LogError(ex, "Failed to publish delivery {DeliveryId}: {Message}",
                delivery.DeliveryId, ex.Message);

            if (_logger.IsEnabled(LogLevel.Trace))
            {
                _logger.LogTrace("Delivery {DeliveryId} exception details: {Exception}",
                    delivery.DeliveryId, ex.ToString());
            }

            // null once MaxRetryAttempts retries are used: the delivery stays Failed for good
            var nextAttempt = _deliveryPolicy.GetNextAttempt(retryCount + 1);
            await _outbox.MarkAsFailedAsync(delivery.DeliveryId, ex.Message, nextAttempt, cancellationToken);
        }
    }
}
