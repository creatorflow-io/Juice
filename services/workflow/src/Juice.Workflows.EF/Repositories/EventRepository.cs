using System.Linq.Expressions;
using Juice.Workflows.Domain.AggregatesModel.EventAggregate;

namespace Juice.Workflows.EF.Repositories
{
    internal class EventRepository<TContext> : IEventRepository
        where TContext : DbContext
    {
        private TContext _dbContext;
        public virtual IQueryable<EventRecord> EventRecords => _dbContext.Set<EventRecord>();
        public EventRepository(TContext dbContext)
        {
            _dbContext = dbContext;
        }
        public async Task<OperationResult> CreateUniqueByWorkflowAsync(EventRecord @event, CancellationToken token)
        {
            try
            {
                var hasKey = !string.IsNullOrEmpty(@event.CatchingKey);
                if (!await EventRecords.AnyAsync(e => e.WorkflowId == @event.WorkflowId
                     && !e.IsCompleted
                     && (e.NodeId == @event.NodeId
                         || (hasKey && e.CatchingKey == @event.CatchingKey)
                         )
                   ))
                {
                    _dbContext.Add(@event);
                    await _dbContext.SaveChangesAsync(token);
                }
                return OperationResult.Success;
            }
            catch (Exception ex)
            {
                return OperationResult.Failed(ex);
            }
        }
        public async Task<EventRecord?> GetAsync(Guid id, CancellationToken token)
            => await EventRecords.FirstOrDefaultAsync(e => e.Id == id, token);
        public async Task<OperationResult> UpdateAsync(EventRecord @event, CancellationToken token)
        {
            try
            {
                _dbContext.Update(@event);
                await _dbContext.SaveChangesAsync(token);
                return OperationResult.Success;
            }
            catch (Exception ex)
            {
                return OperationResult.Failed(ex);
            }
        }

        public async Task<OperationResult> RemoveAsync(EventRecord @event, CancellationToken token)
        {
            try
            {
                _dbContext.Remove(@event);
                await _dbContext.SaveChangesAsync(token);
                return OperationResult.Success;
            }
            catch (Exception ex)
            {
                return OperationResult.Failed(ex);
            }
        }

        public async Task<OperationResult> UpdateStartNodesAsync(string workflowId, EventRecord[] events, CancellationToken token)
        {
            try
            {
                var existingEvents = await EventRecords.Where(e => e.WorkflowId == workflowId && e.IsStartEvent)
                    .ToArrayAsync();

                var removedEvents = existingEvents.Where(e => !events.Any(ne => ne.NodeId.Equals(e.NodeId, StringComparison.OrdinalIgnoreCase)))
                    .ToArray();
                if (removedEvents.Any())
                {
                    _dbContext.RemoveRange(removedEvents);
                }
                var addedEvents = events.Where(e => !existingEvents.Any(oe => oe.NodeId.Equals(e.NodeId, StringComparison.OrdinalIgnoreCase)))
                    .ToArray();

                if (addedEvents.Any())
                {
                    _dbContext.AddRange(addedEvents);
                }

                foreach (var e in existingEvents.Where(e => events.Any(ce => ce.NodeId.Equals(e.NodeId, StringComparison.OrdinalIgnoreCase))))
                {
                    var existingEvent = events.Single(ce => ce.NodeId.Equals(e.NodeId, StringComparison.OrdinalIgnoreCase));
                    e.UpdateDisplayName(existingEvent.DisplayName);
                }

                return OperationResult.Success;
            }
            catch (Exception ex)
            {
                return OperationResult.Failed(ex);
            }
        }

        public async Task<IEnumerable<EventRecord>> FindAllAsync(Expression<Func<EventRecord, bool>> predicate, CancellationToken token)
        {
            return await EventRecords.Where(predicate).ToListAsync();
        }
    }
}
