
namespace Juice.EventBus.IntegrationEventLog
{
    public interface IIntegrationEventRepository : IDisposable
    {
        /// <summary>
        /// Retrieves the collection of integration events that are pending publication for the specified transaction.
        /// </summary>
        /// <param name="transactionId">The identifier of the transaction for which to retrieve pending integration events.</param>
        /// <param name="cancellationToken">A token to monitor for cancellation requests. The operation is canceled if the token is triggered.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains an enumerable collection of <see
        /// cref="IntegrationEvent"/> instances that have not yet been published. If no events are pending, the
        /// collection is empty.</returns>
        ValueTask<IEnumerable<IntegrationEvent>> RetrieveEventsPendingToPublishAsync(Guid transactionId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Asynchronously retrieves a collection of integration events that are pending publication.
        /// </summary>
        /// <param name="take">The maximum number of pending integration events to retrieve. Must be greater than zero.</param>
        /// <param name="tryLimit"></param>
        /// <param name="cancellationToken">A token to monitor for cancellation requests. The operation is canceled if the token is triggered.</param>
        /// <returns>A <see cref="ValueTask{TResult}"/> that represents the asynchronous operation. The result contains an
        /// enumerable of <see cref="IntegrationEvent"/> instances that are pending publication. The collection will be
        /// empty if no events are pending.</returns>
        ValueTask<IEnumerable<IntegrationEvent>> RetrieveEventsPendingToPublishAsync(int take, int tryLimit, CancellationToken cancellationToken = default);

        /// <summary>
        /// Save an integration event within a same transaction with domain DBContext
        /// </summary>
        /// <param name="event"></param>
        /// <param name="transactionId"></param>
        /// <returns></returns>
        ValueTask SaveEventsAsync(Guid transactionId, params IntegrationEvent[] @event);

        /// <summary>
        /// Change event state after publish it to the service bus
        /// </summary>
        /// <param name="eventId"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        ValueTask MarkEventAsPublishedAsync(Guid eventId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Change event state before publish it to the service bus
        /// </summary>
        /// <param name="eventId"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        ValueTask MarkEventAsInProgressAsync(Guid eventId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Change event state on failure publising
        /// </summary>
        /// <param name="eventId"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        ValueTask MarkEventAsFailedAsync(Guid eventId, CancellationToken cancellationToken = default);

    }
    public interface IIntegrationEventRepository<out T> : IIntegrationEventRepository
    {
    }

}
