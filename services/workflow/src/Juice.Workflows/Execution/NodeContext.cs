using Juice.Extensions;

namespace Juice.Workflows.Execution
{
    /// <summary>
    /// Define a running flow object context. It contains IFlowObject instance, Flow object data record.
    /// </summary>
    public class NodeContext
    {
        public NodeContext(NodeRecord record, INode node, Dictionary<string, object?>? properties = default)
        {
            Record = record;
            Node = node;
            if (properties != null)
            {
                Properties.MergeOptions(properties);
            }
        }
        /// <summary>
        /// Node data record
        /// </summary>
        public NodeRecord Record { get; init; }
        /// <summary>
        /// Executable node
        /// </summary>
        public INode Node { get; init; }
        /// <summary>
        /// Display name
        /// </summary>
        public string DisplayName => Record.Name ?? Node.DisplayText.Value;

        public Dictionary<string, object?> Properties { get; set; } = new Dictionary<string, object?>();

        public bool IsStart()
        {
            return Node is StartEvent;
        }
        public bool IsStartOf(string processId)
        {
            return Node is StartEvent && Record.ProcessIdRef == processId;
        }
    }
}
