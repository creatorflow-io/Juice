using Juice.Domain;
using MediatR;
using Newtonsoft.Json.Linq;

namespace Juice.Workflows.Domain.AggregatesModel.WorkflowStateAggregate
{
    /// <summary>
    /// Represents a workflow's serializable runtime state.
    /// </summary>
    public class WorkflowState : IAggregrateRoot<INotification>
    {
        public WorkflowState()
        {

        }
        public WorkflowState(IList<FlowSnapshot> flowSnapshots, IList<NodeSnapshot> nodeSnapshots, IList<ProcessSnapshot> processSnapshots,
            IDictionary<string, object?> output)
        {
            FlowSnapshots = flowSnapshots;
            NodeSnapshots = nodeSnapshots;
            ProcessSnapshots = processSnapshots;
            Output = output;
        }

        public void SetExecutionInfo(IDictionary<string, object?>? input, IEnumerable<string>? lastMessages)
        {
            if (input != null)
            {
                Input = input;
            }
            LastMessages = lastMessages;
        }

        public IEnumerable<string>? LastMessages { get; private set; }

        /// <summary>
        /// A dictionary of input values provided by the caller of the workflow.
        /// </summary>
        public IDictionary<string, object?> Input { get; private set; } = new Dictionary<string, object?>();

        /// <summary>
        /// A dictionary of output values provided by executed nodes of the workflow.
        /// </summary>
        public IDictionary<string, object?> Output { get; init; } = new Dictionary<string, object?>();

        /// <summary>
        /// A dictionary of node states. Each entry contains runtime state for a particular node.
        /// </summary>
        public IDictionary<string, JObject> NodeStates { get; init; } = new Dictionary<string, JObject>();

        /// <summary>
        /// The list of executed nodes.
        /// </summary>
        public IList<NodeSnapshot> NodeSnapshots { get; init; } = new List<NodeSnapshot>();

        /// <summary>
        /// The list of activated flows.
        /// </summary>
        public IList<FlowSnapshot> FlowSnapshots { get; init; } = new List<FlowSnapshot>();

        /// <summary>
        /// Processes's status
        /// </summary>
        public IList<ProcessSnapshot> ProcessSnapshots { get; init; } = new List<ProcessSnapshot>();

        /// <summary>
        /// Blocking nodes could be resume 
        /// </summary>
        public IList<BlockingNode> BlockingNodes => NodeSnapshots.GetBlockingNodes().ToList();

        /// <summary>
        /// Keeps track of which nodes executed in which order.
        /// </summary>
        public IList<ExecutedNode> ExecutedNodes => NodeSnapshots.GetExecutedNodes().ToList();

        /// <summary>
        /// The list of faulted nodes.
        /// </summary>
        public IList<FaultedNode> FaultedNodes => NodeSnapshots.GetFaultedNodes().ToList();

        /// <summary>
        /// Idling nodes could not be execute directly (except catch events)
        /// </summary>
        public IList<IdlingNode>? IdlingNodes { get; init; }

        /// <summary>
        /// Events to dispatch after process
        /// </summary>
        public IList<INotification> DomainEvents { get; init; } = new List<INotification>();
    }
}
