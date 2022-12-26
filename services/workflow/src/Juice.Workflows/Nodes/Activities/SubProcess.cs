using Microsoft.Extensions.DependencyInjection;

namespace Juice.Workflows.Nodes.Activities
{
    public class SubProcess : Activity
    {
        public SubProcess(IServiceProvider serviceProvider, IStringLocalizerFactory stringLocalizer)
            : base(serviceProvider, stringLocalizer)
        {
        }

        public override LocalizedString DisplayText => Localizer["Sub-process"];

        public override LocalizedString Category => Localizer["Sub-Processes"];

        public override async Task<NodeExecutionResult> StartAsync(WorkflowContext workflowContext, NodeContext node, FlowContext? flow, CancellationToken token)
        {
            _logger.LogInformation("Sub-process execute");
            var start = workflowContext.GetStartNode(node.Record.Id);

            var executor = _serviceProvider.GetRequiredService<WorkflowExecutor>();

            await executor.ExecuteAsync(workflowContext, start, token);

            return Halt();
        }

        public override async Task<NodeExecutionResult> ResumeAsync(WorkflowContext workflowContext, NodeContext node, CancellationToken token)
        {
            var end = workflowContext.Nodes.Values.Single(n => n.Node is EndEvent && n.Record.OwnerId == node.Record.Id);
            if (workflowContext.IsFinished(end.Record.Id))
            {
                return Outcomes("Done");
            }
            else if (workflowContext.Nodes.Values.Any(n => n.Record.OwnerId == node.Record.Id
                     && workflowContext.NodeSnapshots.Any(s => s.Id == n.Record.Id && s.Status == WorkflowStatus.Faulted)))
            {
                return Fault("Sub-process error");
            }
            else
            {
                return Halt();
            }
        }
    }
}
