using System.Reflection;
using Juice.EF.Extensions;
using Juice.EventBus.Internal;
using Juice.Measurement;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Newtonsoft.Json;

namespace Juice.EventBus.Transactional.EF
{
    internal class OutboxRepository<TContext> : IOutboxRepository<TContext>, IDisposable
        where TContext : DbContext
    {
        private TContext _domainContext;
        private IOutboxContext _outboxContext;

        private IntegrationEventTypes _eventTypes;

        private ITimeTracker? _timeTracker;

        public OutboxRepository(TContext context, IntegrationEventTypes eventTypes,
            ITimeTracker? timeTracker = default,
            Func<TContext, IOutboxContext>? factory = null)
        {
            _domainContext = context;
            if (factory == null && context is not IOutboxContext)
            {
                throw new ArgumentNullException(nameof(factory), $"When {typeof(TContext).FullName} does not implement {nameof(IOutboxContext)}, a factory method must be provided to create {nameof(IOutboxContext)}.");
            }

            _outboxContext = factory != null ? factory(context) : (IOutboxContext) context;

            _eventTypes = eventTypes;

            _timeTracker = timeTracker;

            if (Assembly.GetEntryAssembly()?.FullName != null)
            {
                var types = Assembly.Load(Assembly.GetEntryAssembly()!.FullName!)
                    .GetTypes()
                    .Where(t => t.IsAssignableTo(typeof(IIntegrationEvent)))
                    .ToList();
                foreach (var type in types)
                {
                    _eventTypes.Register(type);
                }
            }
        }

        /// <summary>
        /// Ensure event log context has an associated connection with input <c>T</c> context.
        /// <para>Throw <see cref="ArgumentException"/> if input context has not same type with <c>TContext</c></para>
        /// </summary>
        private void EnsureAssociatedConnection()
        {
            // If the context is as same type as TContext, no action is needed
            if (_outboxContext.GetType() == typeof(TContext))
            {
                return;
            }
            
            var transaction = _domainContext.GetCurrentTransaction();
            if (transaction == null)
            {
                return;
            }
            try
            {
                // Ensure the context has the same database connection as the domain context
                var connection1 = ((DbContext)_domainContext).Database.GetDbConnection();
                var connection2 = ((DbContext)_outboxContext).Database.GetDbConnection();
                if (!ReferenceEquals(connection1, connection2))
                {
                    ((DbContext)_outboxContext).Database.SetDbConnection(connection1);
                }
                ((DbContext)_outboxContext).Database.UseTransaction(transaction.GetDbTransaction());
            }
            catch (InvalidOperationException ex)
            {
                throw new InvalidOperationException("Please verify that your DBContext is in same scope with IntegrationEventLogContext or call IIntegrationEventLogService.EnsureAssociatedConnection(your DBContext) before.", ex);
            }
        }

        private IIntegrationEvent? GetEvent(OutboxEvent outbox)
        {
            var type = _eventTypes.EventTypes.Find(t => t.Name == outbox.EventTypeName.Split('.').Last());
            if (type == null) { return default; }
            return JsonConvert.DeserializeObject(outbox.Payload, type) as IIntegrationEvent;
        }

        public async ValueTask<IEnumerable<IIntegrationEvent>> RetrieveEventsPendingToPublishAsync(Guid transactionId, CancellationToken cancellationToken = default)
        {
            var tid = transactionId.ToString();

            var result = await _outboxContext.Outbox
            .AsNoTracking()
            .Where(e => e.TransactionId == tid && e.State == EventState.NotPublished).ToListAsync(cancellationToken);

            if (result != null && result.Any())
            {
                return result.OrderBy(o => o.CreationTime)
                    .Select(e => GetEvent(e))
                    .OfType<IIntegrationEvent>();
            }

            return [];
        }

        public async ValueTask<IEnumerable<IIntegrationEvent>> RetrieveEventsPendingToPublishAsync(int take, int tryLimit, CancellationToken cancellationToken = default)
        {

            var result = await _outboxContext.Outbox.AsNoTracking()
                .Where(e =>
                    e.State == EventState.NotPublished
                    || ((e.State == EventState.InProgress || e.State == EventState.PublishedFailed)
                        && e.ProcessedOn < DateTime.UtcNow.AddMinutes(-5)
                        && e.TimesSent <= tryLimit)
                 )
                .OrderBy(e => e.ProcessedOn)
                .Take(take)
                .ToListAsync(cancellationToken);

            if (result != null && result.Any())
            {
                return result.OrderBy(o => o.CreationTime)
                    .Select(e => GetEvent(e))
                    .OfType<IIntegrationEvent>();
            }

            return [];
        }

        public async ValueTask SaveEventsAsync(Guid transactionId, IIntegrationEvent[] @events)
        {
            EnsureAssociatedConnection();
            _timeTracker?.Checkpoint("EnsureAssociatedConnection");

            foreach (var @event in @events)
            {
                _eventTypes.Register(@event.GetType());
                var eventLogEntry = new OutboxEvent {
                    EventId = @event.Id,
                    CreationTime = @event.CreationDate,
                    EventTypeName = @event.GetType().FullName!,
                    Payload = JsonConvert.SerializeObject(@event),
                    TransactionId = transactionId.ToString()
                };
                _outboxContext.Outbox.Add(eventLogEntry);
            }
            await ((DbContext)_outboxContext).SaveChangesAsync();
            _timeTracker?.Checkpoint("SaveChanges");
        }

        public ValueTask MarkEventAsPublishedAsync(Guid eventId, CancellationToken cancellationToken = default)
        {
            return UpdateEventStatusAsync(eventId, EventState.Published, default, cancellationToken);
        }

        public ValueTask MarkEventAsInProgressAsync(Guid eventId, CancellationToken cancellationToken = default)
        {
            return UpdateEventStatusAsync(eventId, EventState.InProgress, default, cancellationToken);
        }

        public ValueTask MarkEventAsFailedAsync(Guid eventId, string error, CancellationToken cancellationToken = default)
        {
            return UpdateEventStatusAsync(eventId, EventState.PublishedFailed, error, cancellationToken);
        }

        private async ValueTask UpdateEventStatusAsync(Guid eventId, EventState state, string? error, CancellationToken cancellationToken)
        {
            var eventLogEntry = await _outboxContext.Outbox.SingleAsync(ie => ie.EventId == eventId);
            eventLogEntry.UpdateState(state, error);

            await ((DbContext)_outboxContext).SaveChangesAsync(cancellationToken);
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
