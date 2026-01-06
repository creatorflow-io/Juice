
namespace Juice.EventBus.IntegrationEventLog
{
    public interface IIntegrationEventLogService : IDisposable
    {
        /// <summary>
        /// Use to process pending events
        /// </summary>
        /// <param name="transactionId"></param>
        /// <returns></returns>
        Task<IEnumerable<IntegrationEvent>> RetrieveEventLogsPendingToPublishAsync(Guid? transactionId);

        /// <summary>
        /// Save an integration event within a same transaction with domain DBContext
        /// </summary>
        /// <param name="event"></param>
        /// <param name="transactionId"></param>
        /// <returns></returns>
        Task AddEventAsync(IntegrationEvent @event, Guid transactionId);

        /// <summary>
        /// Change event state after publish it to the service bus
        /// </summary>
        /// <param name="eventId"></param>
        /// <returns></returns>
        Task MarkEventAsPublishedAsync(Guid eventId);

        /// <summary>
        /// Change event state before publish it to the service bus
        /// </summary>
        /// <param name="eventId"></param>
        /// <returns></returns>
        Task MarkEventAsInProgressAsync(Guid eventId);

        /// <summary>
        /// Change event state on failure publising
        /// </summary>
        /// <param name="eventId"></param>
        /// <returns></returns>
        Task MarkEventAsFailedAsync(Guid eventId);

    }
    public interface IIntegrationEventLogService<out T> : IIntegrationEventLogService
    {
    }

}
