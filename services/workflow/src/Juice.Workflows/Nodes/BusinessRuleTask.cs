namespace Juice.Workflows.Nodes
{
    public class BusinessRuleTask : Activity
    {
        public BusinessRuleTask(ILoggerFactory logger,
            IStringLocalizer<BusinessRuleTask> stringLocalizer)
            : base(logger, stringLocalizer)
        {
        }

        public override LocalizedString DisplayText => Localizer["Business Rule Task"];

        public override Task<NodeExecutionResult> ExecuteAsync(WorkflowContext workflowContext, NodeContext node, FlowContext? flow, CancellationToken token)
            => Task.FromResult(Outcomes("Done"));
    }
}
