namespace Juice.Workflows.Nodes.Events
{
    public class BoundaryTimerEvent : BoundaryEvent
    {
        public BoundaryTimerEvent(IStringLocalizerFactory stringLocalizer) : base(stringLocalizer)
        {
        }

        public override LocalizedString DisplayText => Localizer["Timer Event"];

        public override IEnumerable<Outcome> GetPossibleOutcomes(WorkflowContext workflowContext, NodeContext node)
            => new List<Outcome> { new Outcome(Localizer["Throwed"]) };

        public override Task<bool> PreStartCheckAsync(WorkflowContext workflowContext, NodeContext node, NodeContext ancestor, CancellationToken token)
        {
            return Task.FromResult(true);
        }

        public override Task<NodeExecutionResult> StartAsync(WorkflowContext workflowContext, NodeContext node, FlowContext? flow, CancellationToken token)
        {
            // Should register a timer
            return Task.FromResult(Halt());
        }
        public override Task<NodeExecutionResult> ResumeAsync(WorkflowContext workflowContext, NodeContext node, CancellationToken token)
            => Task.FromResult(Outcomes("Throwed"));
    }
}
