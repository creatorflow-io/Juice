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
        /// Change the state of several deliveries after publishing them, in one commit where the repository can.
        /// </summary>
        async ValueTask MarkAsPublishedAsync(IReadOnlyCollection<Guid> deliveryIds, CancellationToken cancellationToken = default)
        {
            foreach (var deliveryId in deliveryIds)
            {
                await MarkAsPublishedAsync(deliveryId, cancellationToken);
            }
        }

        /// <summary>
        /// Change event state before publish it to the service bus.
        /// Claiming a <see cref="DeliveryState.Failed"/> delivery increments <see cref="OutboxDelivery.RetryCount"/>.
        /// </summary>
        /// <param name="deliveryId"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        ValueTask<int> MarkAsInProgressAsync(Guid deliveryId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Claim several deliveries, in one commit where the repository can.
        /// Claiming a <see cref="DeliveryState.Failed"/> delivery increments <see cref="OutboxDelivery.RetryCount"/>.
        /// </summary>
        /// <returns>The deliveries this call claimed. The others were claimed by another worker.</returns>
        async ValueTask<IReadOnlyCollection<Guid>> MarkAsInProgressAsync(IReadOnlyCollection<Guid> deliveryIds, CancellationToken cancellationToken = default)
        {
            var claimed = new List<Guid>(deliveryIds.Count);
            foreach (var deliveryId in deliveryIds)
            {
                if (await MarkAsInProgressAsync(deliveryId, cancellationToken) > 0)
                {
                    claimed.Add(deliveryId);
                }
            }
            return claimed;
        }

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
