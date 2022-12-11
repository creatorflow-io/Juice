using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Juice.EventBus.IntegrationEventLog.EF
{
    internal class IntegrationEventLogService : IIntegrationEventLogService, IDisposable
    {
        public IntegrationEventLogContext LogContext => _integrationEventLogContext;
        private readonly IntegrationEventLogContext _integrationEventLogContext;
        private volatile bool disposedValue;
        private readonly IntegrationEventTypes _eventTypes;

        public IntegrationEventLogService(IntegrationEventLogContext integrationEventLogContext, IntegrationEventTypes eventTypes)
        {
            _eventTypes = eventTypes;
            _integrationEventLogContext = integrationEventLogContext;

            var types = Assembly.Load(Assembly.GetEntryAssembly().FullName)
                .GetTypes()
                .Where(t => t.Name.EndsWith(nameof(IntegrationEvent)))
                .ToList();
            foreach (var type in types)
            {
                _eventTypes.Register(type);
            }
        }

        public async Task<IEnumerable<IntegrationEventLogEntry>> RetrieveEventLogsPendingToPublishAsync(Guid transactionId)
        {
            var tid = transactionId.ToString();

            var result = await _integrationEventLogContext.IntegrationEventLogs
                .Where(e => e.TransactionId == tid && e.State == EventState.NotPublished).ToListAsync();

            if (result != null && result.Any())
            {
                return result.OrderBy(o => o.CreationTime)
                    .Select(e => e.DeserializeJsonContent(_eventTypes.EventTypes.Find(t => t.Name == e.EventTypeShortName)));
            }

            return new List<IntegrationEventLogEntry>();
        }

        public Task SaveEventAsync(IntegrationEvent @event, IDbContextTransaction transaction)
        {
            if (transaction == null) throw new ArgumentNullException(nameof(transaction));

            _eventTypes.Register(@event.GetType());
            var eventLogEntry = new IntegrationEventLogEntry(@event, transaction.TransactionId);

            try
            {
                _integrationEventLogContext.Database.UseTransaction(transaction.GetDbTransaction());
            }
            catch (InvalidOperationException ex)
            {
                throw new InvalidOperationException("Please verify that your DBContext is in same scope with IntegrationEventLogContext or call IIntegrationEventLogService.EnsureAssociatedConnection(your DBContext) before.", ex);
            }
            _integrationEventLogContext.IntegrationEventLogs.Add(eventLogEntry);

            return _integrationEventLogContext.SaveChangesAsync();
        }

        public Task MarkEventAsPublishedAsync(Guid eventId)
        {
            return UpdateEventStatus(eventId, EventState.Published);
        }

        public Task MarkEventAsInProgressAsync(Guid eventId)
        {
            return UpdateEventStatus(eventId, EventState.InProgress);
        }

        public Task MarkEventAsFailedAsync(Guid eventId)
        {
            return UpdateEventStatus(eventId, EventState.PublishedFailed);
        }

        private Task UpdateEventStatus(Guid eventId, EventState status)
        {
            var eventLogEntry = _integrationEventLogContext.IntegrationEventLogs.Single(ie => ie.EventId == eventId);
            eventLogEntry.State = status;

            if (status == EventState.InProgress)
                eventLogEntry.TimesSent++;

            _integrationEventLogContext.IntegrationEventLogs.Update(eventLogEntry);

            return _integrationEventLogContext.SaveChangesAsync();
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    _integrationEventLogContext?.Dispose();
                }


                disposedValue = true;
            }
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
