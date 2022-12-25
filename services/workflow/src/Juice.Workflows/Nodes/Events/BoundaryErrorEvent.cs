namespace Juice.Workflows.Nodes.Events
{
    public class BoundaryErrorEvent : BoundaryEvent
    {
        private ILogger _logger;
        public BoundaryErrorEvent(ILogger<BoundaryErrorEvent> logger, IStringLocalizer<BoundaryErrorEvent> stringLocalizer) : base(stringLocalizer)
        {
            _logger = logger;
        }

        public override LocalizedString DisplayText => Localizer["Error"];

        public override Task<bool> PreStartCheckAsync(WorkflowContext workflowContext, NodeContext node, NodeContext ancestor, CancellationToken token)
        {
            if (ancestor == null)
            {
                throw new ArgumentNullException("ancestor");
            }

            var status = workflowContext.NodeSnapshots.Where(s => s.Id == ancestor.Record.Id).First().Status;
            if (status == WorkflowStatus.Faulted)
            {
                return Task.FromResult(true);
            }
            return Task.FromResult(false);
        }

        public override Task<NodeExecutionResult> StartAsync(WorkflowContext workflowContext, NodeContext node, FlowContext? flow, CancellationToken token)
        {
            _logger.LogInformation("Throw boundary error event");
            return Task.FromResult(Outcomes("Throwed"));
        }

    }
}
