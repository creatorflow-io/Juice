using Juice.Workflows.Domain.AggregatesModel.EventAggregate;

namespace Juice.Workflows.EF.Repositories
{
    internal class EventRepository : IEventRepository
    {
        private WorkflowDbContext _dbContext;
        public EventRepository(WorkflowDbContext dbContext)
        {
            _dbContext = dbContext;
        }
        public async Task<OperationResult> CreateAsync(EventRecord @event, CancellationToken token)
        {
            try
            {
                _dbContext.EventRecords.Add(@event);
                await _dbContext.SaveChangesAsync(token);
                return OperationResult.Success;
            }
            catch (Exception ex)
            {
                return OperationResult.Failed(ex);
            }
        }
        public async Task<EventRecord?> GetAsync(Guid id, CancellationToken token)
            => await _dbContext.EventRecords.FirstOrDefaultAsync(e => e.Id == id, token);
        public async Task<OperationResult> UpdateAsync(EventRecord @event, CancellationToken token)
        {
            try
            {
                _dbContext.EventRecords.Update(@event);
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
                _dbContext.EventRecords.Remove(@event);
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
                var existingEvents = await _dbContext.EventRecords.Where(e => e.WorkflowId == workflowId && e.IsStartEvent)
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
    }
}
