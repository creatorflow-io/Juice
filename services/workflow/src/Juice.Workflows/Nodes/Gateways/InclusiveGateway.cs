namespace Juice.Workflows.Nodes.Gateways
{
    public class InclusiveGateway : Gateway
    {
        public InclusiveGateway(IStringLocalizer<InclusiveGateway> stringLocalizer) : base(stringLocalizer)
        {
        }

        public override LocalizedString DisplayText => Localizer["Inclusive Gateway"];

        public override async Task<NodeExecutionResult> StartAsync(WorkflowContext workflowContext, NodeContext node, FlowContext? flow, CancellationToken token)
        {
            if (flow == null)
            {
                return Fault("InclusiveGateway required atleast one incoming flow");
            }
            if (workflowContext.AnyIncompleteActivePathTo(node))
            {
                return Noop("InclusiveGateway must has completed for each activated of the incoming sequence flows");
            }

            return JoinnedOutcomes(workflowContext, node);
        }
        public override Task<NodeExecutionResult> ResumeAsync(WorkflowContext workflowContext, NodeContext node, CancellationToken token) => throw new NotImplementedException();
    }
}
