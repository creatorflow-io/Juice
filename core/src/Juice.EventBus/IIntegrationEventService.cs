namespace Juice.EventBus
{
    public interface IIntegrationEventService
    {
        /// <summary>
        /// Adds the specified integration event to the list of events to be saved and published
        /// </summary>
        /// <param name="evt"></param>
        /// <returns></returns>
        ValueTask AddEventAsync(IIntegrationEvent evt);

        /// <summary>
        /// Persists all pending events associated with the specified transaction to the underlying event store
        /// asynchronously.
        /// </summary>
        /// <remarks>This method does not commit the transaction itself; it only saves the events linked
        /// to the provided <paramref name="transactionId"/>.</remarks>
        /// <param name="transactionId">The identifier of the transaction whose events should be saved.</param>
        /// <returns>A <see cref="Task"/> that represents the asynchronous save operation.</returns>
        ValueTask SaveEventsAsync(Guid? transactionId);

        /// <summary>
        /// Publish events that are pending to be published
        /// </summary>
        /// <param name="transactionId"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        ValueTask PublishEventsThroughEventBusAsync(Guid transactionId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Publishes up to the specified number of pending events through the event bus asynchronously.
        /// </summary>
        /// <param name="maxEvents">The maximum number of events to publish in this operation. Must be greater than zero.</param>
        /// <param name="maxTries"></param>
        /// <param name="cancellationToken">A token to monitor for cancellation requests. The operation is canceled if the token is triggered.</param>
        /// <returns>A <see cref="ValueTask"/> that represents the asynchronous publish operation.</returns>
        ValueTask PublishEventsThroughEventBusAsync(int maxEvents, int maxTries, CancellationToken cancellationToken = default);

    }
    public interface IIntegrationEventService<out TContext> : IIntegrationEventService
    {

    }

    public interface IIntegrationEventService<out TContext, out TEventBus>: IIntegrationEventService<TContext>
        where TEventBus : IEventBus
    {
    }
}
