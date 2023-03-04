using System.Linq.Expressions;

namespace Juice.Workflows.Domain.AggregatesModel.EventAggregate
{
    public interface IEventRepository
    {
        Task<OperationResult> CreateUniqueByWorkflowAsync(EventRecord @event, CancellationToken token);
        Task<OperationResult> UpdateAsync(EventRecord @event, CancellationToken token);
        Task<EventRecord?> GetAsync(Guid id, CancellationToken token);
        Task<OperationResult> RemoveAsync(EventRecord @event, CancellationToken token);
        Task<OperationResult> UpdateStartNodesAsync(string workflowId, EventRecord[] events, CancellationToken token);
        Task<IEnumerable<EventRecord>> FindAllAsync(Expression<Func<EventRecord, bool>> predicate, CancellationToken token);
    }
}
