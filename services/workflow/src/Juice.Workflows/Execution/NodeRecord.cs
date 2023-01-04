namespace Juice.Workflows.Execution
{
    /// <summary>
    /// A record of node on a workflow process
    /// </summary>
    public record NodeRecord
    {
        public NodeRecord() { }
        public NodeRecord(string id, string name)
        {
            Id = id;
            Name = name;
        }

        public NodeRecord(string id, string name, string processIdRef) : this(id, name)
        {
            ProcessIdRef = processIdRef;
        }

        /// <summary>
        /// Generated id of node
        /// </summary>
        public string Id { get; init; }

        /// <summary>
        /// The display name of node.
        /// </summary>
        public string Name { get; init; }

        /// <summary>
        /// Outgoing flows
        /// </summary>
        public string[] Outgoings { get; private set; } = Array.Empty<string>();
        /// <summary>
        /// Incoming flows
        /// </summary>
        public string[] Incomings { get; private set; } = Array.Empty<string>();

        /// <summary>
        /// Process/sub-process reference
        /// </summary>
        public string ProcessIdRef { get; init; }

        /// <summary>
        /// Default outgoing flow
        /// </summary>
        public string? Default { get; private set; }
        /// <summary>
        /// Boundary event reference
        /// </summary>
        public string? AttachedToRef { get; private set; }

        public void SetDefault(string flowId)
        {
            Default = flowId;
        }

        public void AddOutgoing(string flowId)
        {
            if (!Outgoings.Contains(flowId))
            {
                Outgoings = Outgoings.Append(flowId).ToArray();
            }
        }

        public void AddIncoming(string flowId)
        {
            if (!Incomings.Contains(flowId))
            {
                Incomings = Incomings.Append(flowId).ToArray();
            }
        }

        public NodeRecord AttachTo(string nodeId)
        {
            AttachedToRef = nodeId;
            return this;
        }

    }
}
