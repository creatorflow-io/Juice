namespace Juice.Workflows.Nodes.Events
{
    public class TimerIntermediateCatchEvent : IntermediateCatchEvent
    {
        public TimerIntermediateCatchEvent(IStringLocalizerFactory stringLocalizer) : base(stringLocalizer)
        {
        }

        public override LocalizedString DisplayText => Localizer["Timer Intermediate Catch Event"];

        public override Task<NodeExecutionResult> StartAsync(WorkflowContext workflowContext, NodeContext node, FlowContext? flow, CancellationToken token)
        {
            workflowContext.AddDomainEvent(new TimerEventStartDomainEvent(node));
            return base.StartAsync(workflowContext, node, flow, token);
        }
    }
}
