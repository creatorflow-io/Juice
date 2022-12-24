namespace Juice.Workflows.Execution
{
    /// <summary>
    /// Define a running flow context. It contains IFlow instance, Flow object data record.
    /// </summary>
    public class FlowContext
    {
        public FlowContext(FlowRecord record, IFlow flow)
        {
            Record = record;
            Flow = flow;
        }
        /// <summary>
        /// Node data record
        /// </summary>
        public FlowRecord Record { get; init; }
        /// <summary>
        /// Executable node
        /// </summary>
        public IFlow Flow { get; init; }
        /// <summary>
        /// Display name
        /// </summary>
        public string DisplayName => Record.Name ?? Record.ConditionExpression ?? "";
    }
}
