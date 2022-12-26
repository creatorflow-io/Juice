namespace Juice.Workflows.Domain.AggregatesModel.WorkflowAggregate
{
    /// <summary>
    /// A record of node on a workflow process
    /// </summary>
    public class NodeRecord
    {
        /// <summary>
        /// Generated id of node
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// The type of node.
        /// </summary>
        public string Name { get; set; }

        public string[] Outgoings { get; set; } = Array.Empty<string>();
        public string[] Incomings { get; set; } = Array.Empty<string>();

        public string? OwnerId { get; set; }

        public string? Default { get; set; }
        public string? AttachedToRef { get; set; }
    }
}
