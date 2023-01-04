namespace Juice.Workflows.Nodes.Events
{
    public abstract class IntermediateThrowEvent : Event, IIntermediate, IThrowing
    {
        protected IntermediateThrowEvent(IStringLocalizerFactory stringLocalizer) : base(stringLocalizer)
        {
        }

        public override async Task<NodeExecutionResult> StartAsync(WorkflowContext workflowContext, NodeContext node, FlowContext? flow, CancellationToken token)
        {
            return Outcomes("Throwed");
        }

        public override IEnumerable<Outcome> GetPossibleOutcomes(WorkflowContext workflowContext, NodeContext node)
            => new Outcome[] { new Outcome(Localizer["Throwed"]) };
    }
}
