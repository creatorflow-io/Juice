namespace Juice.Workflows.Nodes
{
    public class ReceiveTask : Activity
    {
        public ReceiveTask(ILoggerFactory logger,
            IStringLocalizer<ReceiveTask> stringLocalizer)
            : base(logger, stringLocalizer)
        {
        }

        public override LocalizedString DisplayText => Localizer["Receive Task"];

        public override Task<NodeExecutionResult> ResumeAsync(WorkflowContext workflowContext, NodeContext node, CancellationToken token)
            => Task.FromResult(Outcomes("Received"));

        public override IEnumerable<Outcome> GetPossibleOutcomes(WorkflowContext workflowContext, NodeContext node) => new Outcome[] { new Outcome(Localizer["Received"]) };
    }
}
