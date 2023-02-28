namespace Juice.Workflows.Domain.AggregatesModel.DefinitionAggregate
{
    public record NodeData
    {
        public NodeData(NodeRecord node, string typeName, bool isStart, Dictionary<string, object> properties)
        {
            NodeRecord = node;
            TypeName = typeName;
            IsStart = isStart;
            Properties = properties;
        }

        public bool IsStart { get; init; }

        public NodeRecord NodeRecord { get; init; }

        public Dictionary<string, object> Properties { get; init; }

        public string TypeName { get; init; }
    }
}
