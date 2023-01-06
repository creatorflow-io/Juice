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
        public List<string> Outgoings { get; init; } = new List<string>();
        /// <summary>
        /// Incoming flows
        /// </summary>
        public List<string> Incomings { get; init; } = new List<string>();

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
        private string? _attachedToRef;
        public string? AttachedToRef { get { return _attachedToRef; } init { _attachedToRef = value; } }

        public void SetDefault(string flowId)
        {
            Default = flowId;
        }

        public void AddOutgoing(string flowId)
        {
            if (!Outgoings.Contains(flowId))
            {
                Outgoings.Add(flowId);
            }
        }

        public void AddIncoming(string flowId)
        {
            if (!Incomings.Contains(flowId))
            {
                Incomings.Add(flowId);
            }
        }

        public NodeRecord AttachTo(string nodeId)
        {
            _attachedToRef = nodeId;
            return this;
        }

    }
}
