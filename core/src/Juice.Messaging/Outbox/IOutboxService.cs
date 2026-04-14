namespace Juice.Messaging.Outbox
{
    public interface IOutboxService
    {
        /// <summary>
        /// Adds the specified integration event to the list of events to be saved and published
        /// </summary>
        /// <param name="message"></param>
        /// <returns></returns>
        ValueTask AddEventAsync(IMessage message);

        /// <summary>
        /// Persists all pending events associated with the specified transaction to the underlying event store
        /// asynchronously.
        /// </summary>
        /// <remarks>This method does not commit the transaction itself; it only saves the events linked
        /// to the provided <paramref name="transactionId"/>.</remarks>
        /// <param name="transactionId">The identifier of the transaction whose events should be saved.</param>
        /// <param name="cancellationToken"></param>
        /// <returns>A <see cref="Task"/> that represents the asynchronous save operation.</returns>
        ValueTask SaveEventsAsync(Guid? transactionId, CancellationToken cancellationToken = default);

    }
    public interface IOutboxService<out TContext> : IOutboxService
    {
        /// <summary>
        /// Returns the outbox delivery IDs created for the given <paramref name="messageId"/>
        /// and <paramref name="publisherKey"/> during the most recent
        /// <see cref="IOutboxService.SaveEventsAsync"/> call.
        /// Returns an empty list if no matching deliveries were created, or if
        /// <see cref="IOutboxService.SaveEventsAsync"/> has not yet been called.
        /// This snapshot is reset at the start of each new <see cref="IOutboxService.SaveEventsAsync"/> cycle.
        /// </summary>
        IReadOnlyList<Guid> GetPendingDeliveryIds(Guid messageId, string publisherKey);
    }
}
