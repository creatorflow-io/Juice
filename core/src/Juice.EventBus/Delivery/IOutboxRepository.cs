namespace Juice.EventBus.Delivery
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
        /// Change event state before publish it to the service bus
        /// </summary>
        /// <param name="deliveryId"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        ValueTask<int> MarkAsInProgressAsync(Guid deliveryId, CancellationToken cancellationToken = default);

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
