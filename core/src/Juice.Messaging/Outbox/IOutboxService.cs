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

    }
}
