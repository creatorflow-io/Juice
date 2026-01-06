using System.Reflection;
using Microsoft.EntityFrameworkCore;

namespace Juice.EventBus.IntegrationEventLog.EF
{
    internal class IntegrationEventRepository<TContext> : IIntegrationEventRepository<TContext>, IDisposable
        where TContext : DbContext, IIntegrationEventLogDbContext
    {

        private TContext _context;

        private IntegrationEventTypes _eventTypes;

        public IntegrationEventRepository(TContext context, IntegrationEventTypes eventTypes)
        {
            _context = context;
            _eventTypes = eventTypes;
            if (Assembly.GetEntryAssembly()?.FullName != null)
            {
                var types = Assembly.Load(Assembly.GetEntryAssembly()!.FullName!)
                    .GetTypes()
                    .Where(t => t.Name.EndsWith(nameof(IntegrationEvent)))
                    .ToList();
                foreach (var type in types)
                {
                    _eventTypes.Register(type);
                }
            }
        }

        public async ValueTask<IEnumerable<IntegrationEvent>> RetrieveEventsPendingToPublishAsync(Guid transactionId, CancellationToken cancellationToken = default)
        {
            var tid = transactionId.ToString();

            var result = await _context.IntegrationEventLogs
            .AsNoTracking()
            .Where(e => e.TransactionId == tid && e.State == EventState.NotPublished).ToListAsync(cancellationToken);

            if (result != null && result.Any())
            {
                return result.OrderBy(o => o.CreationTime)
                    .Select(e =>
                        {
                            e.DeserializeJsonContent(_eventTypes.EventTypes.Find(t => t.Name == e.EventTypeShortName));
                            return e.IntegrationEvent!;
                        });
            }

            return [];
        }

        public async ValueTask<IEnumerable<IntegrationEvent>> RetrieveEventsPendingToPublishAsync(int take, int tryLimit, CancellationToken cancellationToken = default)
        {

            var result = await _context.IntegrationEventLogs.AsNoTracking()
                .Where(e =>
                    e.State == EventState.NotPublished
                    || ((e.State == EventState.InProgress || e.State == EventState.PublishedFailed)
                        && e.ModificationTime < DateTime.UtcNow.AddMinutes(-5)
                        && e.TimesSent <= tryLimit)
                 )
                .OrderBy(e => e.ModificationTime)
                .Take(take)
                .ToListAsync(cancellationToken);

            if (result != null && result.Any())
            {
                return result.OrderBy(o => o.CreationTime)
                    .Select(e =>
                    {
                        e.DeserializeJsonContent(_eventTypes.EventTypes.Find(t => t.Name == e.EventTypeShortName));
                        return e.IntegrationEvent!;
                    });
            }

            return [];
        }


        public async ValueTask SaveEventsAsync(Guid transactionId, IntegrationEvent[] @event)
        {
            foreach (var evt in @event)
            {
                _eventTypes.Register(evt.GetType());
                var eventLogEntry = new IntegrationEventLogEntry(evt, transactionId);
                _context.IntegrationEventLogs.Add(eventLogEntry);
            }
            await _context.SaveChangesAsync();
        }

        public ValueTask MarkEventAsPublishedAsync(Guid eventId, CancellationToken cancellationToken = default)
        {
            return UpdateEventStatusAsync(eventId, EventState.Published, cancellationToken);
        }

        public ValueTask MarkEventAsInProgressAsync(Guid eventId, CancellationToken cancellationToken = default)
        {
            return UpdateEventStatusAsync(eventId, EventState.InProgress, cancellationToken);
        }

        public ValueTask MarkEventAsFailedAsync(Guid eventId, CancellationToken cancellationToken = default)
        {
            return UpdateEventStatusAsync(eventId, EventState.PublishedFailed, cancellationToken);
        }

        private async ValueTask UpdateEventStatusAsync(Guid eventId, EventState state, CancellationToken cancellationToken)
        {
            var eventLogEntry = await _context.IntegrationEventLogs.SingleAsync(ie => ie.EventId == eventId);
            eventLogEntry.UpdateState(state);

            await _context.SaveChangesAsync(cancellationToken);
        }

        #region IDisposable Support
        private volatile bool disposedValue;

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                _eventTypes = null!;

                disposedValue = true;
            }
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
        #endregion
    }
}
