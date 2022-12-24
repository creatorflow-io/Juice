namespace Juice.Workflows.Nodes
{
    public class EventBasedGateway : Gateway, IExclusive, IEventBased
    {
        private ILogger _logger;
        public EventBasedGateway(ILogger<EventBasedGateway> logger,
            IStringLocalizer<EventBasedGateway> stringLocalizer) : base(stringLocalizer)
        {
            _logger = logger;
        }

        public override LocalizedString DisplayText => Localizer["Event-based Gateway"];


        public override async Task<NodeExecutionResult> ExecuteAsync(WorkflowContext workflowContext, NodeContext node, FlowContext? flow, CancellationToken token)
        {
            _logger.LogInformation(node.Record.Name + " execute");
            if (flow == null)
            {
                return Fault("EventBasedGateway required single incoming flow");
            }
            if (workflowContext.AnyActiveFlowTo(node, flow.Record.Id))
            {
                return Fault("EventBasedGateway must has single active incoming flow");
            }

            return SourceOutcomes(workflowContext, flow);
        }

        public override Task<NodeExecutionResult> ResumeAsync(WorkflowContext workflowContext, NodeContext node, CancellationToken token) => throw new NotImplementedException();

        public override async Task PostCheckAsync(WorkflowContext workflowContext, NodeContext node, CancellationToken token)
        {
            _logger.LogInformation(node.Record.Name + " post check");
            if (!workflowContext.AnyActiveFlowFrom(node))
            {
                throw new InvalidOperationException("No sequence flow can be selected. To ensure a sequence flow will always be selected, have no condition on one of your flows");
            }

            await base.PostCheckAsync(workflowContext, node, token);
        }

    }
}
