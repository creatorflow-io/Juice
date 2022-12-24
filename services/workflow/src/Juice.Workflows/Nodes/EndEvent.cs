namespace Juice.Workflows.Nodes
{
    public class EndEvent : Event
    {
        private ILogger _logger;
        public EndEvent(ILogger<EndEvent> logger, IStringLocalizer<EndEvent> stringLocalizer) : base(stringLocalizer)
        {
            _logger = logger;
        }

        public override LocalizedString DisplayText => Localizer["End Event"];


        public override IEnumerable<Outcome> GetPossibleOutcomes(WorkflowContext workflowContext, NodeContext node)
            => new List<Outcome> { new Outcome(Localizer["Throwed"]) };

        public override Task<NodeExecutionResult> ExecuteAsync(WorkflowContext workflowContext, NodeContext node, FlowContext? flowContext, CancellationToken token)
        {
            _logger.LogInformation(node.Record.Name + " throwed");
            return Task.FromResult(Outcomes("Throwed"));
        }

    }
}
