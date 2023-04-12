using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Juice.EventBus.IntegrationEventLog.EF
{
    public interface IIntegrationEventLogService : IDisposable
    {
        IntegrationEventLogContext LogContext { get; }

        /// <summary>
        /// Use to process pending events
        /// </summary>
        /// <param name="transactionId"></param>
        /// <returns></returns>
        Task<IEnumerable<IntegrationEventLogEntry>> RetrieveEventLogsPendingToPublishAsync(Guid transactionId);

        /// <summary>
        /// Save an integration event within a same transaction with domain DBContext
        /// </summary>
        /// <param name="event"></param>
        /// <param name="transaction"></param>
        /// <returns></returns>
        Task SaveEventAsync(IntegrationEvent @event, IDbContextTransaction transaction);

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
    public interface IIntegrationEventLogService<out TContext> : IIntegrationEventLogService
        where TContext : DbContext
    {
        /// <summary>
        /// Ensure event log context has an associated connection with input <see cref="T"/> context.
        /// <para>Throw <see cref="ArgumentException"/> if input context has not same type with <see cref="TContext"/></para>
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="context"></param>
        void EnsureAssociatedConnection<T>(T context) where T : DbContext;
    }

}
