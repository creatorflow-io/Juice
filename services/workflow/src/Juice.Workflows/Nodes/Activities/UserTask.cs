namespace Juice.Workflows.Nodes.Activities
{
    public class UserTask : Activity
    {
        public override LocalizedString DisplayText => Localizer["User Task"];

        public UserTask(IServiceProvider serviceProvider, IStringLocalizerFactory stringLocalizer)
            : base(serviceProvider, stringLocalizer)
        {
        }

        public override Task<NodeExecutionResult> StartAsync(WorkflowContext workflowContext, NodeContext node, FlowContext? flow, CancellationToken token)
        {
            workflowContext.AddDomainEvent(new UserTaskRequestDomainEvent(node));
            return base.StartAsync(workflowContext, node, flow, token);
        }

        public override Task<NodeExecutionResult> ResumeAsync(WorkflowContext workflowContext, NodeContext node, CancellationToken token)
        {
            workflowContext.AddDomainEvent(new UserTaskCompleteDomainEvent(node));
            return base.ResumeAsync(workflowContext, node, token);
        }
    }
}
