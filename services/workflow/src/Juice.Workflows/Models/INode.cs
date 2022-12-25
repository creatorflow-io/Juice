namespace Juice.Workflows.Models
{
    public interface INode : IDisposable
    {
        LocalizedString DisplayText { get; }
        LocalizedString Category { get; }


        /// <summary>
        /// List of possible outcomes when the activity is executed.
        /// </summary>
        IEnumerable<Outcome> GetPossibleOutcomes(WorkflowContext workflowContext, NodeContext node);

        /// <summary>
        /// Executes the specified flow object.
        /// </summary>
        Task<NodeExecutionResult> StartAsync(WorkflowContext workflowContext, NodeContext node,
            FlowContext? flow,
            CancellationToken token);

        /// <summary>
        /// Resume the specified activity.
        /// </summary>
        Task<NodeExecutionResult> ResumeAsync(WorkflowContext workflowContext, NodeContext node,
            CancellationToken token);

    }

    public interface IEvent : INode
    {
    }

    public interface IIntermediate : IEvent
    {

    }

    public interface IThrowing : IEvent
    {

    }

    public interface IBoundary : IThrowing, IIntermediate
    {
        /// <summary>
        /// Check before start.
        /// </summary>
        Task<bool> PreStartCheckAsync(WorkflowContext workflowContext, NodeContext node, NodeContext ancestor,
            CancellationToken token);

    }

    public interface ICatching : IEvent
    {

    }

    public interface IActivity : INode
    {

    }

    public interface IGateway : INode
    {
        /// <summary>
        /// Check after executed.
        /// </summary>
        Task PostExecuteCheckAsync(WorkflowContext workflowContext, NodeContext node,
            CancellationToken token);
    }

    public interface IExclusive : IGateway { }
    public interface IEventBased : IGateway { }
}
