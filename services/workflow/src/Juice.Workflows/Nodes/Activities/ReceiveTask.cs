namespace Juice.Workflows.Nodes.Activities
{
    public class ReceiveTask : Activity
    {
        public ReceiveTask(IServiceProvider serviceProvider,
            IStringLocalizer<ReceiveTask> stringLocalizer)
            : base(serviceProvider, stringLocalizer)
        {
        }

        public override LocalizedString DisplayText => Localizer["Receive Task"];

        public override Task<NodeExecutionResult> ResumeAsync(WorkflowContext workflowContext, NodeContext node, CancellationToken token)
            => Task.FromResult(Outcomes("Received"));

        public override IEnumerable<Outcome> GetPossibleOutcomes(WorkflowContext workflowContext, NodeContext node) => new Outcome[] { new Outcome(Localizer["Received"]) };
    }
}
