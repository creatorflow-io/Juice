namespace Juice.Workflows.Nodes.Events
{
    public class TerminateEvent : EndEvent
    {
        public TerminateEvent(ILoggerFactory logger, IStringLocalizerFactory stringLocalizer) : base(logger, stringLocalizer)
        {
        }

        public override LocalizedString DisplayText => Localizer["Terminate Event"];


        public override IEnumerable<Outcome> GetPossibleOutcomes(WorkflowContext workflowContext, NodeContext node)
            => new List<Outcome> { new Outcome(Localizer["Throwed"]) };

        public override Task<NodeExecutionResult> StartAsync(WorkflowContext workflowContext, NodeContext node, FlowContext? flowContext, CancellationToken token)
        {
            if (string.IsNullOrEmpty(node.Record.OwnerId))
            {
                workflowContext.AddDomainEvent(new WorkflowFinishedDomainEvent(node, WorkflowStatus.Aborted));
                workflowContext.Terminate();
            }
            _logger.LogInformation(node.Record.Name + " throwed");
            return Task.FromResult<NodeExecutionResult>(new(WorkflowStatus.Aborted, "Throwed"));
        }

    }
}
