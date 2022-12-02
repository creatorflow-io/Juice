using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Juice.EventBus.IntegrationEventLog.EF
{
    internal class IntegrationEventLogService<TContext> : IIntegrationEventLogService<TContext>
        where TContext : DbContext
    {
        private IIntegrationEventLogService _integrationEventLogService;

        public IntegrationEventLogContext LogContext => _integrationEventLogService.LogContext;

        private Func<TContext, IntegrationEventLogContext> _factory;

        public IntegrationEventLogService(Func<TContext, IntegrationEventLogContext> factory, TContext context)
        {
            _integrationEventLogService = new IntegrationEventLogService(factory(context));
            _factory = factory;
        }

        public void EnsureAssociatedConnection<T>(T context) where T : DbContext
        {
            if (!(context is TContext tContext))
            {
                throw new ArgumentException($"Input context must be an instance of {typeof(TContext).Name}");
            }
            _integrationEventLogService.Dispose();
            _integrationEventLogService = new IntegrationEventLogService(_factory(tContext));
        }

        void IDisposable.Dispose() => _integrationEventLogService.Dispose();
        Task IIntegrationEventLogService.MarkEventAsFailedAsync(Guid eventId)
            => _integrationEventLogService.MarkEventAsFailedAsync(eventId);
        Task IIntegrationEventLogService.MarkEventAsInProgressAsync(Guid eventId)
            => _integrationEventLogService.MarkEventAsInProgressAsync(eventId);
        Task IIntegrationEventLogService.MarkEventAsPublishedAsync(Guid eventId)
            => _integrationEventLogService.MarkEventAsPublishedAsync(eventId);
        Task<IEnumerable<IntegrationEventLogEntry>> IIntegrationEventLogService.RetrieveEventLogsPendingToPublishAsync(Guid transactionId)
            => _integrationEventLogService.RetrieveEventLogsPendingToPublishAsync(transactionId);
        Task IIntegrationEventLogService.SaveEventAsync(IntegrationEvent @event, IDbContextTransaction transaction)
            => _integrationEventLogService.SaveEventAsync(@event, transaction);
    }
}
