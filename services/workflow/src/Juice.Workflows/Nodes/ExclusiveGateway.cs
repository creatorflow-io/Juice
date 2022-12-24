namespace Juice.Workflows.Nodes
{
    public class ExclusiveGateway : Gateway, IExclusive
    {
        private ILogger _logger;
        public ExclusiveGateway(ILogger<ExclusiveGateway> logger, IStringLocalizer<ExclusiveGateway> stringLocalizer) : base(stringLocalizer)
        {
            _logger = logger;
        }

        public override LocalizedString DisplayText => Localizer["Exclusive Gateway"];

        public override async Task<NodeExecutionResult> ExecuteAsync(WorkflowContext workflowContext, NodeContext node, FlowContext? flow, CancellationToken token)
        {
            _logger.LogInformation(node.Record.Name + " execute");
            if (flow == null)
            {
                return Fault("ExclusiveGateway required single incoming flow");
            }
            if (workflowContext.AnyActiveFlowTo(node, flow.Record.Id))
            {
                return Fault("ExclusiveGateway must has single active incoming flow");
            }

            return SourceOutcomes(workflowContext, flow);
        }

        public override Task<NodeExecutionResult> ResumeAsync(WorkflowContext workflowContext, NodeContext node, CancellationToken token) => throw new NotImplementedException();

        public override Task PostCheckAsync(WorkflowContext workflowContext, NodeContext node, CancellationToken token)
        {
            _logger.LogInformation(node.Record.Name + " post check");
            if (!workflowContext.AnyActiveFlowFrom(node))
            {
                throw new InvalidOperationException("No sequence flow can be selected. To ensure a sequence flow will always be selected, have no condition on one of your flows");
            }

            return base.PostCheckAsync(workflowContext, node, token);
        }

    }
}
