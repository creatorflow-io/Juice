using Newtonsoft.Json.Linq;

namespace Juice.Workflows.Execution
{
    /// <summary>
    /// Represents a workflow's serializable runtime state.
    /// </summary>
    public class WorkflowState
    {
        public WorkflowState()
        {

        }
        public WorkflowState(IList<FlowSnapshot> flowSnapshots, IList<NodeSnapshot> nodeSnapshots, IDictionary<string, object?> output)
        {
            FlowSnapshots = flowSnapshots;
            NodeSnapshots = nodeSnapshots;
            Output = output;
        }

        public IEnumerable<string>? LastMessages { get; set; }

        /// <summary>
        /// A dictionary of input values provided by the caller of the workflow.
        /// </summary>
        public IDictionary<string, object?> Input { get; set; } = new Dictionary<string, object?>();

        /// <summary>
        /// A dictionary of output values provided by executed nodes of the workflow.
        /// </summary>
        public IDictionary<string, object?> Output { get; set; } = new Dictionary<string, object?>();

        /// <summary>
        /// A dictionary of node states. Each entry contains runtime state for a particular node.
        /// </summary>
        public IDictionary<string, JObject> NodeStates { get; set; } = new Dictionary<string, JObject>();

        /// <summary>
        /// The list of executed nodes.
        /// </summary>
        public IList<NodeSnapshot> NodeSnapshots { get; set; } = new List<NodeSnapshot>();

        /// <summary>
        /// The list of activated flows.
        /// </summary>
        public IList<FlowSnapshot> FlowSnapshots { get; set; } = new List<FlowSnapshot>();

        /// <summary>
        /// Blocking nodes could be resume 
        /// </summary>
        public IList<BlockingNode>? BlockingNodes { get; init; }

        /// <summary>
        /// Keeps track of which nodes executed in which order.
        /// </summary>
        public IList<ExecutedNode>? ExecutedNodes { get; init; }

        /// <summary>
        /// The list of faulted nodes.
        /// </summary>
        public IList<FaultedNode>? FaultedNodes { get; init; }

        /// <summary>
        /// Idling nodes could not be execute directly (except catch events)
        /// </summary>
        public IList<IdlingNode>? IdlingNodes { get; init; }

        /// <summary>
        /// Events are listening to dispatch process
        /// </summary>
        public IList<ListeningEvent>? ListeningEvents { get; init; }
    }
}
