using Juice.Extensions;

namespace Juice.Workflows.Nodes.Activities
{
    public class ServiceTask : Activity
    {
        public override LocalizedString DisplayText => Localizer["Service Task"];

        public ServiceTask(IServiceProvider serviceProvider, IStringLocalizerFactory stringLocalizer)
            : base(serviceProvider, stringLocalizer)
        {
        }

        public override Task<NodeExecutionResult> StartAsync(WorkflowContext workflowContext, NodeContext node, FlowContext? flow, CancellationToken token)
        {
            workflowContext.AddDomainEvent(new ServiceTaskRequestDomainEvent(node));
            return base.StartAsync(workflowContext, node, flow, token);
        }

        public override Task<NodeExecutionResult> ResumeAsync(WorkflowContext workflowContext, NodeContext node, CancellationToken token)
        {
            var status = workflowContext.Input.GetOption<WorkflowStatus?>("TaskStatus");
            if (status != null && (status == WorkflowStatus.Faulted || status == WorkflowStatus.Aborted))
            {
                return Task.FromResult(Fault($"Service responsed \"{status.DisplayValue()}\" status"));
            }

            workflowContext.AddDomainEvent(new ServiceTaskCompleteDomainEvent(node));
            return base.ResumeAsync(workflowContext, node, token);
        }
    }
}
