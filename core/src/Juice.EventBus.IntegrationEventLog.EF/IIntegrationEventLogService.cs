using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Juice.EventBus.IntegrationEventLog.EF
{
    public interface IIntegrationEventLogService : IDisposable
    {
        IntegrationEventLogContext LogContext { get; }
        Task<IEnumerable<IntegrationEventLogEntry>> RetrieveEventLogsPendingToPublishAsync(Guid transactionId);
        Task SaveEventAsync(IntegrationEvent @event, IDbContextTransaction transaction);
        Task MarkEventAsPublishedAsync(Guid eventId);
        Task MarkEventAsInProgressAsync(Guid eventId);
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
