namespace Juice.Workflows.Domain.AggregatesModel.DefinitionAggregate
{
    public record FlowData
    {
        public FlowData(FlowRecord flowRecord, string typeName)
        {
            FlowRecord = flowRecord;
            TypeName = typeName;
        }

        public FlowRecord FlowRecord { get; init; }
        public string TypeName { get; init; }
    }
}
