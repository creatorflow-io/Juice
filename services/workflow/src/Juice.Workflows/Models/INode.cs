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
        Task<NodeExecutionResult> ExecuteAsync(WorkflowContext workflowContext, NodeContext node,
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

    public interface ICatching : IEvent
    {

    }

    public interface IActivity : INode
    {

    }

    public interface IGateway : INode
    {
        /// <summary>
        /// Executes the specified flow object.
        /// </summary>
        Task PostCheckAsync(WorkflowContext workflowContext, NodeContext node,
            CancellationToken token);

    }

    public interface IExclusive : IGateway { }
    public interface IEventBased : IGateway { }
}
