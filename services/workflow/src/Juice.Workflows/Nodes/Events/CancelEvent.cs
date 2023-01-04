namespace Juice.Workflows.Nodes.Events
{
    public class CancelEvent : EndEvent
    {
        public CancelEvent(ILoggerFactory logger, IStringLocalizerFactory stringLocalizer) : base(logger, stringLocalizer)
        {
        }

        public override LocalizedString DisplayText => Localizer["Cancel Event"];


        public override IEnumerable<Outcome> GetPossibleOutcomes(WorkflowContext workflowContext, NodeContext node)
            => new List<Outcome> { new Outcome(Localizer["Throwed"]) };

        public override Task<NodeExecutionResult> StartAsync(WorkflowContext workflowContext, NodeContext node, FlowContext? flowContext, CancellationToken token)
        {
            if (workflowContext.Processes.Any(p => p.Id == node.Record.ProcessIdRef))
            {
                // if EndEvent is not inside sub-process
                workflowContext.AddDomainEvent(new WorkflowFinishedDomainEvent(node, WorkflowStatus.Aborted));
                workflowContext.Terminate(node.Record.ProcessIdRef);
            }

            _logger.LogInformation(node.Record.Name + " throwed");
            return Task.FromResult<NodeExecutionResult>(new(WorkflowStatus.Aborted, "Throwed"));
        }

    }
}
