namespace Juice.Workflows.Nodes.Events
{
    public class BoundaryTimerEvent : BoundaryEvent
    {
        private ILogger _logger;
        public BoundaryTimerEvent(ILogger<BoundaryErrorEvent> logger, IStringLocalizerFactory stringLocalizer) : base(stringLocalizer)
        {
            _logger = logger;
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
            _logger.LogDebug("Registed a timer");
            workflowContext.AddDomainEvent(new TimerEventStartDomainEvent(workflowContext.WorkflowId, node));
            return Task.FromResult(Halt());
        }

        public override Task<NodeExecutionResult> ResumeAsync(WorkflowContext workflowContext, NodeContext node, CancellationToken token)
        {
            _logger.LogInformation("Timed out");
            return Task.FromResult(Outcomes("Throwed"));
        }
    }
}
