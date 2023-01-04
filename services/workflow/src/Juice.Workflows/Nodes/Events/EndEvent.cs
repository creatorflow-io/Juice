namespace Juice.Workflows.Nodes.Events
{
    public class EndEvent : Event, IThrowing
    {
        protected ILogger _logger;
        public EndEvent(ILoggerFactory logger, IStringLocalizerFactory stringLocalizer) : base(stringLocalizer)
        {
            _logger = logger.CreateLogger(GetType());
        }

        public override LocalizedString DisplayText => Localizer["End Event"];


        public override IEnumerable<Outcome> GetPossibleOutcomes(WorkflowContext workflowContext, NodeContext node)
            => new List<Outcome> { new Outcome(Localizer["Throwed"]) };

        public override Task<NodeExecutionResult> StartAsync(WorkflowContext workflowContext, NodeContext node, FlowContext? flowContext, CancellationToken token)
        {
            if (workflowContext.Processes.Any(p => p.Id == node.Record.ProcessIdRef))
            {
                // if EndEvent is not inside sub-process
                workflowContext.AddDomainEvent(new WorkflowFinishedDomainEvent(node, WorkflowStatus.Finished));
                workflowContext.Finish(node.Record.ProcessIdRef);
            }
            _logger.LogInformation(node.Record.Name + " throwed");
            return Task.FromResult(Outcomes("Throwed"));
        }

    }
}
