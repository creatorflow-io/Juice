using Juice.EF.Extensions;
using Juice.EventBus.Delivery;
using Juice.Measurement;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Juice.EventBus.Transactional.EF
{
    internal class OutboxRepository<TContext> : IOutboxRepository<TContext>, IDisposable
        where TContext : DbContext
    {
        private TContext _domainContext;
        private IOutboxContext _outboxContext;

        private ITimeTracker? _timeTracker;

        public OutboxRepository(TContext context,
            ITimeTracker? timeTracker = default,
            Func<TContext, IOutboxContext>? factory = null)
        {
            _domainContext = context;
            if (factory == null && context is not IOutboxContext)
            {
                throw new ArgumentNullException(nameof(factory), $"When {typeof(TContext).FullName} does not implement {nameof(IOutboxContext)}, a factory method must be provided to create {nameof(IOutboxContext)}.");
            }

            _outboxContext = factory != null ? factory(context) : (IOutboxContext)context;

            _timeTracker = timeTracker;
        }
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
        public async ValueTask SaveEventsAsync(OutboxEvent[] @events, CancellationToken cancellationToken)
        {
            if(@events == null || @events.Length == 0)
            {
                return;
            }
            EnsureAssociatedConnection();
            _timeTracker?.Checkpoint("EnsureAssociatedConnection");
            await _outboxContext.Outbox.AddRangeAsync(@events, cancellationToken);
            await ((DbContext)_outboxContext).SaveChangesAsync(cancellationToken);
            _timeTracker?.Checkpoint("SaveChanges");
        }

        public async ValueTask MarkAsPublishedAsync(Guid deliveryId, CancellationToken cancellationToken = default)
        {
            var query = _outboxContext.OutboxDeliveries
                .Where(ie => ie.DeliveryId == deliveryId && ie.State != DeliveryState.Published);
            await query.ExecuteUpdateAsync(ie =>
                ie.SetProperty(e => e.State, e => DeliveryState.Published)
                  .SetProperty(e => e.ProcessedOn, e => DateTimeOffset.Now)
                , cancellationToken);
        }

        public async ValueTask<int> MarkAsInProgressAsync(Guid deliveryId, CancellationToken cancellationToken = default)
        {
            var query = _outboxContext.OutboxDeliveries
                .Where(ie => ie.DeliveryId == deliveryId && (ie.State == DeliveryState.NotPublished || ie.State == DeliveryState.Failed));

            return await query.ExecuteUpdateAsync(ie =>
                ie.SetProperty(e => e.State, e => DeliveryState.InProgress)
                  .SetProperty(e => e.ProcessedOn, e => DateTimeOffset.Now)
                , cancellationToken);
        }

        public async ValueTask MarkAsFailedAsync(Guid deliveryId, string error,
            DateTimeOffset? nextAttempt,
            CancellationToken cancellationToken = default)
        {
            var query = _outboxContext.OutboxDeliveries
                .Where(ie => ie.DeliveryId == deliveryId && ie.State != DeliveryState.Published && ie.State != DeliveryState.Skipped);
            await query.ExecuteUpdateAsync(ie =>
                ie.SetProperty(e => e.State, e => DeliveryState.Failed)
                  .SetProperty(e => e.LastError, e => error)
                  .SetProperty(e => e.NextAttemptOn, e => nextAttempt)
                  .SetProperty(e => e.ProcessedOn, e => DateTimeOffset.Now)
                , cancellationToken);
        }

        public async ValueTask MarkAsSkippedAsync(Guid deliveryId, string reason, CancellationToken cancellationToken = default)
        {
            var query = _outboxContext.OutboxDeliveries
                .Where(ie => ie.DeliveryId == deliveryId && ie.State != DeliveryState.Published && ie.State != DeliveryState.Skipped);
            await query.ExecuteUpdateAsync(ie =>
                ie.SetProperty(e => e.State, e => DeliveryState.Skipped)
                  .SetProperty(e => e.LastError, e => reason)
                  .SetProperty(e => e.ProcessedOn, e => DateTimeOffset.Now)
                , cancellationToken);
        }

        #region IDisposable Support
        private volatile bool disposedValue;

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
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
