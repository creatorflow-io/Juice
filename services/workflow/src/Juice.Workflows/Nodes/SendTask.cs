namespace Juice.Workflows.Nodes
{
    public class SendTask : Activity
    {
        public SendTask(ILoggerFactory logger, IStringLocalizer<SendTask> stringLocalizer)
            : base(logger, stringLocalizer)
        {
        }

        public override LocalizedString DisplayText => Localizer["Send Task"];

        public override Task<NodeExecutionResult> ExecuteAsync(WorkflowContext workflowContext, NodeContext node, FlowContext? flow, CancellationToken token)
            => Task.FromResult(Outcomes("Sent"));

        public override Task<NodeExecutionResult> ResumeAsync(WorkflowContext workflowContext, NodeContext node, CancellationToken token) => throw new NotImplementedException();

        public override IEnumerable<Outcome> GetPossibleOutcomes(WorkflowContext workflowContext, NodeContext node) => new Outcome[] { new Outcome(Localizer["Sent"]) };
    }
}
