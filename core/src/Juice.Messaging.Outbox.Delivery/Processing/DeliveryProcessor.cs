using System;
using System.Collections.Concurrent;
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

        /// <summary>
        /// Claims the deliveries in one commit, sends them, then marks them published in one commit: a commit waits for the
        /// disk, and one per message is what limits delivery. A crash after sending leaves the batch
        /// <see cref="DeliveryState.InProgress"/> for recover-timeout, which sends it again; delivery is at least once.
        /// </summary>
        /// <returns>False when a delivery failed, or nothing was left to work on.</returns>
        public async Task<bool> ProcessAsync(OutboxDelivery[] deliveries, CancellationToken cancellationToken = default)
        {
            EnsureConfigured();

            var clean = true;
            var attempted = false;

            // InProgress here means recover-timeout picked up a delivery whose worker never finished it
            foreach (var delivery in deliveries.Where(d => d.State == DeliveryState.InProgress))
            {
                attempted = true;
                clean &= await ProcessReclaimedAsync(delivery, cancellationToken);
            }

            var pending = new List<OutboxDelivery>(deliveries.Length);
            foreach (var delivery in deliveries.Where(d => d.State != DeliveryState.InProgress))
            {
                if (delivery.State == DeliveryState.Published)
                {
                    _logger.LogWarning("Delivery {DeliveryId} already published, skipping", delivery.DeliveryId);
                    continue;
                }
                pending.Add(delivery);
            }

            if (pending.Count > 0)
            {
                attempted = true;
                clean &= await ProcessPendingAsync(pending, cancellationToken);
            }

            return clean && attempted;
        }

        private void EnsureConfigured()
        {
            if (_publisher is null || _deliveryPolicy is null)
            {
                throw new InvalidOperationException(
                    "DeliveryProcessor not configured. Call Configure() before ProcessAsync().");
            }
        }

        private async Task<bool> ProcessPendingAsync(List<OutboxDelivery> pending, CancellationToken cancellationToken)
        {
            var claimedIds = (await _outbox.MarkAsInProgressAsync(pending.Select(d => d.DeliveryId).ToArray(), cancellationToken)).ToHashSet();
            if (claimedIds.Count < pending.Count)
            {
                _logger.LogDebug("{Count} of {Total} deliveries were claimed by another worker", pending.Count - claimedIds.Count, pending.Count);
            }

            // In the order retrieved, so the messages of one transaction go out in the order they were created. A message
            // without a transaction depends on nothing.
            var groups = pending.Where(d => claimedIds.Contains(d.DeliveryId))
                .GroupBy(d => d.OutboxEvent?.TransactionId ?? d.DeliveryId.ToString())
                .Select(g => g.ToArray())
                .ToArray();

            var published = new ConcurrentBag<Guid>();
            var failures = new ConcurrentBag<DeliveryFailure>();

            async ValueTask SendGroupAsync(OutboxDelivery[] group, CancellationToken token)
            {
                foreach (var delivery in group)
                {
                    // RetryCount as stored once this worker has claimed the delivery
                    var retryCount = delivery.State == DeliveryState.NotPublished ? delivery.RetryCount : delivery.RetryCount + 1;
                    var failure = await SendAsync(delivery, retryCount, token);
                    if (failure is null)
                    {
                        published.Add(delivery.DeliveryId);
                    }
                    else
                    {
                        failures.Add(failure);
                    }
                }
            }

            try
            {
                if (_publisher.SupportsConcurrentPublish && _deliveryPolicy.MaxConcurrency > 1 && groups.Length > 1)
                {
                    var options = new ParallelOptions
                    {
                        MaxDegreeOfParallelism = _deliveryPolicy.MaxConcurrency,
                        CancellationToken = cancellationToken
                    };
                    await Parallel.ForEachAsync(groups, options, SendGroupAsync);
                }
                else
                {
                    foreach (var group in groups)
                    {
                        await SendGroupAsync(group, cancellationToken);
                    }
                }
            }
            finally
            {
                // Also when stopping: what the broker has must not be sent again. After the sends, because they share
                // nothing with the repository's context and this does.
                await _outbox.MarkAsPublishedAsync(published.ToArray(), CancellationToken.None);
                foreach (var failure in failures)
                {
                    await HandleDeliveryFailureAsync(failure.Delivery, failure.RetryCount, failure.Exception, CancellationToken.None);
                }
            }

            return failures.IsEmpty;
        }

        private async Task<bool> ProcessReclaimedAsync(OutboxDelivery delivery, CancellationToken cancellationToken)
        {
            var retryCount = delivery.RetryCount + 1;
            try
            {
                var affected = await _outbox.ReclaimInProgressAsync(delivery.DeliveryId, delivery.ProcessedOn, cancellationToken);
                if (affected == 0)
                {
                    _logger.LogInformation("Delivery {DeliveryId} is already being processed by another worker", delivery.DeliveryId);
                    return true;
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to reclaim delivery {DeliveryId}: {Message}", delivery.DeliveryId, ex.Message);
                await HandleDeliveryFailureAsync(delivery, delivery.RetryCount, ex, cancellationToken);
                DeliveryMetrics.IncrementDeliveryFailure(delivery.PublisherKey, ex.GetType().Name);
                return false;
            }

            if (retryCount > _deliveryPolicy.MaxRetryAttempts)
            {
                await AbandonAsync(delivery, retryCount, cancellationToken);
                return false;
            }

            var failure = await SendAsync(delivery, retryCount, cancellationToken);
            if (failure is not null)
            {
                await HandleDeliveryFailureAsync(delivery, retryCount, failure.Exception, cancellationToken);
                return false;
            }
            await _outbox.MarkAsPublishedAsync(delivery.DeliveryId, cancellationToken);
            return true;
        }

        // A delivery that hangs or crashes its worker must not be reclaimed forever
        private async Task AbandonAsync(OutboxDelivery delivery, int retryCount, CancellationToken cancellationToken)
        {
            using var activity = MessagingActivity.StartDelivery(delivery.PublisherKey,
                delivery.OutboxEvent?.EventTypeName, delivery.EventId.ToString(), delivery.OutboxEvent?.Headers);
            _logger.LogError("Delivery {DeliveryId} did not finish after {RetryCount} attempts; giving up", delivery.DeliveryId, retryCount);
            await _outbox.MarkAsFailedAsync(delivery.DeliveryId,
                $"Abandoned: the delivery did not finish within {_deliveryPolicy.Timeout} after {retryCount} attempts", null, cancellationToken);
            DeliveryMetrics.IncrementDeliveryFailure(delivery.PublisherKey, "Abandoned");
            activity?.SetTag(MessagingActivity.Tags.DeliveryOutcome, "abandoned");
            activity?.SetStatus(ActivityStatusCode.Error, "Abandoned");
            MessagingMetrics.RecordOutboxError(delivery.PublisherKey, delivery.OutboxEvent?.EventTypeName, "Abandoned");
        }

        /// <summary>
        /// Sends one claimed delivery. It touches no shared state, so several can run at once, and leaves the repository to
        /// the caller.
        /// <para>
        /// For the <c>"local"</c> publisher: if phase 1 (immediate dispatch via
        /// <c>LocalChannelBackgroundService</c>) succeeded and marked the record <c>Published</c>,
        /// <c>SendPendingIntent</c> never retrieves it (it filters to <c>NotPublished</c> only),
        /// so this method is not called for those records. This path handles phase 1 failures,
        /// crash-recovery scenarios, and any case where the outbox record was not closed by phase 1.
        /// </para>
        /// </summary>
        /// <returns>The failure, or null when the broker has the message.</returns>
        private async Task<DeliveryFailure?> SendAsync(OutboxDelivery delivery, int retryCount, CancellationToken cancellationToken)
        {
            // "TraceId" is the job key of the Juice.Extensions.Logging providers (one file / log group per delivery),
            // not the OpenTelemetry trace id
            using var _ = _logger.BeginScope(new Dictionary<string, object?> { { "TraceId", delivery.DeliveryId } });
            using var activity = MessagingActivity.StartDelivery(delivery.PublisherKey,
                delivery.OutboxEvent?.EventTypeName, delivery.EventId.ToString(), delivery.OutboxEvent?.Headers);
            var sw = Stopwatch.StartNew();
            try
            {
                DeliveryMetrics.IncrementDeliveryAttempt(delivery.PublisherKey);
                activity?.SetTag(MessagingActivity.Tags.RetryCount, retryCount);
                await PublishEventAsync(delivery, cancellationToken);

                DeliveryMetrics.IncrementDeliverySuccess(delivery.PublisherKey);
                activity?.SetTag(MessagingActivity.Tags.DeliveryOutcome, "published");
                MessagingMetrics.RecordOutboxPublished(delivery.PublisherKey, delivery.OutboxEvent?.EventTypeName);

                if (_logger.IsEnabled(LogLevel.Debug))
                {
                    _logger.LogDebug("Successfully published delivery {DeliveryId}", delivery.DeliveryId);
                }
                return null;
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
                DeliveryMetrics.IncrementDeliveryFailure(delivery.PublisherKey, ex.GetType().Name);
                _logger.LogError(ex, "Failed to publish delivery {DeliveryId}: {Message}", delivery.DeliveryId, ex.Message);
                return new DeliveryFailure(delivery, retryCount, ex);
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
            if (_logger.IsEnabled(LogLevel.Trace))
            {
                _logger.LogTrace("Delivery {DeliveryId} exception details: {Exception}",
                    delivery.DeliveryId, ex.ToString());
            }

            // null once MaxRetryAttempts retries are used: the delivery stays Failed for good
            var nextAttempt = _deliveryPolicy.GetNextAttempt(retryCount + 1);
            await _outbox.MarkAsFailedAsync(delivery.DeliveryId, ex.Message, nextAttempt, cancellationToken);
        }

        private sealed record DeliveryFailure(OutboxDelivery Delivery, int RetryCount, Exception Exception);
    }
}
