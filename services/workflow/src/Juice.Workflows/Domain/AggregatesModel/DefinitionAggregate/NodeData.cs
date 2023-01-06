namespace Juice.Workflows.Domain.AggregatesModel.DefinitionAggregate
{
    public record NodeData
    {
        public NodeData(NodeRecord node, string typeName)
        {
            NodeRecord = node;
            TypeName = typeName;
        }

        public NodeRecord NodeRecord { get; init; }
        public string TypeName { get; init; }
    }
}
