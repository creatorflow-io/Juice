namespace Juice.Workflows.Nodes
{
    public class SubProcess : Activity
    {
        private WorkflowExecutor _executor;
        public SubProcess(WorkflowExecutor executor, ILoggerFactory logger, IStringLocalizer<SubProcess> stringLocalizer)
            : base(logger, stringLocalizer)
        {
            _executor = executor;
        }

        public override LocalizedString DisplayText => Localizer["Sub-process"];

        public override LocalizedString Category => Localizer["Sub-Processes"];

        public override async Task<NodeExecutionResult> ExecuteAsync(WorkflowContext workflowContext, NodeContext node, FlowContext? flow, CancellationToken token)
        {
            _logger.LogInformation("Sub-process execute");
            var start = workflowContext.GetStartNode(node.Record.Id);

            await _executor.ExecuteAsync(workflowContext, start.Record.Id, token);

            return Halt();
        }

        public override async Task<NodeExecutionResult> ResumeAsync(WorkflowContext workflowContext, NodeContext node, CancellationToken token)
        {

            var end = workflowContext.Nodes.Values.Single(n => n.Node is EndEvent && n.Record.OwnerId == node.Record.Id);
            if (workflowContext.IsFinished(end.Record.Id))
            {
                return Outcomes("Done");
            }
            else
            {
                return Halt();
            }
        }
    }
}
