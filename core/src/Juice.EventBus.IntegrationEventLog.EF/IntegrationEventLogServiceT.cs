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

        private TContext _context;

        public TContext DomainContext => _context;

        private IntegrationEventTypes _eventTypes;

        public IntegrationEventLogService(Func<TContext, IntegrationEventLogContext> factory, TContext context, IntegrationEventTypes eventTypes)
        {
            _context = context;
            _integrationEventLogService = new IntegrationEventLogService(factory(context), eventTypes);
            _factory = factory;
            _eventTypes = eventTypes;
        }

        public void EnsureAssociatedConnection<T>(T context) where T : DbContext
        {
            if (!(context is TContext tContext))
            {
                throw new ArgumentException($"Input context must be an instance of {typeof(TContext).Name}");
            }
            _context = tContext;
            _integrationEventLogService.Dispose();
            _integrationEventLogService = new IntegrationEventLogService(_factory(tContext), _eventTypes);
        }


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


        #region IDisposable Support
        private bool _disposedValue;

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                if (disposing)
                {
                    // dispose managed state (managed objects)
                    _integrationEventLogService.Dispose();
                    _integrationEventLogService = null!;
                }

                // TODO: free unmanaged resources (unmanaged objects) and override finalizer
                // set large fields to null
                _context = null!;
                _eventTypes = null!;
                _factory = null!;
                _disposedValue = true;
            }
        }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
        #endregion
    }
}
