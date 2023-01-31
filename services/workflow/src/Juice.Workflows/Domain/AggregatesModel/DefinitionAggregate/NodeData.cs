namespace Juice.Workflows.Domain.AggregatesModel.DefinitionAggregate
{
    public record NodeData
    {
        public NodeData(NodeRecord node, string typeName, Dictionary<string, object> properties)
        {
            NodeRecord = node;
            TypeName = typeName;
            Properties = properties;
        }

        public NodeRecord NodeRecord { get; init; }

        public Dictionary<string, object> Properties { get; init; }

        public string TypeName { get; init; }
    }
}
