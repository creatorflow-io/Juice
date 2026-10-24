namespace Juice.Messaging.Outbox
{
    public interface IOutboxRepository : IDisposable
    {
        /// <summary>
        /// Save outbox events
        /// </summary>
        /// <param name="event"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        ValueTask SaveEventsAsync(OutboxEvent[] @event, CancellationToken cancellationToken = default);

        /// <summary>
        /// Change event state after publish it to the service bus
        /// </summary>
        /// <param name="deliveryId"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        ValueTask MarkAsPublishedAsync(Guid deliveryId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Change event state before publish it to the service bus.
        /// Claiming a <see cref="DeliveryState.Failed"/> delivery increments <see cref="OutboxDelivery.RetryCount"/>.
        /// </summary>
        /// <param name="deliveryId"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        ValueTask<int> MarkAsInProgressAsync(Guid deliveryId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Re-claim a delivery left <see cref="DeliveryState.InProgress"/> by a worker that stopped or timed out.
        /// Succeeds only while the row still carries <paramref name="staleProcessedOn"/>, so exactly one worker wins.
        /// A successful reclaim counts as a retry and increments <see cref="OutboxDelivery.RetryCount"/>.
        /// </summary>
        /// <param name="deliveryId"></param>
        /// <param name="staleProcessedOn">The <see cref="OutboxDelivery.ProcessedOn"/> value read with the delivery</param>
        /// <param name="cancellationToken"></param>
        /// <returns>Number of rows claimed (0 or 1)</returns>
        /// <exception cref="NotSupportedException">The repository does not implement recovery. The delivery is then
        /// marked failed with this error and picked up by retry-failed instead.</exception>
        ValueTask<int> ReclaimInProgressAsync(Guid deliveryId, DateTimeOffset? staleProcessedOn, CancellationToken cancellationToken = default)
            => throw new NotSupportedException($"{GetType().FullName} does not implement {nameof(ReclaimInProgressAsync)}; stuck InProgress deliveries cannot be recovered by recover-timeout.");

        /// <summary>
        /// Change event state on failure publising
        /// </summary>
        /// <param name="deliveryId"></param>
        /// <param name="error"></param>
        /// <param name="nextAttempt"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        ValueTask MarkAsFailedAsync(Guid deliveryId, string error,
            DateTimeOffset? nextAttempt,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Change event state to skipped
        /// </summary>
        /// <param name="deliveryId"></param>
        /// <param name="reason"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        ValueTask MarkAsSkippedAsync(Guid deliveryId, string reason,
            CancellationToken cancellationToken = default);

    }

    public interface IOutboxRepository<T> : IOutboxRepository
    {
    }

}
