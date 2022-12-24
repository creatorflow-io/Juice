using Newtonsoft.Json.Linq;

namespace Juice.Workflows.Execution
{
    /// <summary>
    /// Define a running flow object context. It contains IFlowObject instance, Flow object data record.
    /// </summary>
    public class NodeContext
    {
        public NodeContext(NodeRecord record, INode node, JObject? properties = default)
        {
            Record = record;
            Node = node;
            if (properties != null)
            {
                Properties.Merge(properties);
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

        public JObject Properties { get; set; } = new JObject();

    }
}
