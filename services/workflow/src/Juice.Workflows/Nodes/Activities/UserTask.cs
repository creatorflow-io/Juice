using Juice.Workflows.Domain.Commands;
using Microsoft.Extensions.DependencyInjection;

namespace Juice.Workflows.Nodes.Activities
{
    public class UserTask : Activity
    {
        public override LocalizedString DisplayText => Localizer["User Task"];

        public UserTask(IServiceProvider serviceProvider, IStringLocalizerFactory stringLocalizer)
            : base(serviceProvider, stringLocalizer)
        {
        }

        public override async Task<NodeExecutionResult> StartAsync(WorkflowContext workflowContext, NodeContext node, FlowContext? flow, CancellationToken token)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
                var rs = await mediator.Send(new StartUserTaskCommand(workflowContext.WorkflowId, workflowContext.CorrelationId, node));
                if (rs.Succeeded)
                {
                    workflowContext.AddDomainEvent(new UserTaskRequestDomainEvent(node));

                    return await base.StartAsync(workflowContext, node, flow, token);
                }
                return Fault(rs.Message ?? "Failed to start an user task");
            }
            catch (Exception ex)
            {
                return Fault(ex.Message);
            }
        }

        public override Task<NodeExecutionResult> ResumeAsync(WorkflowContext workflowContext, NodeContext node, CancellationToken token)
        {
            workflowContext.AddDomainEvent(new UserTaskCompleteDomainEvent(node));
            return base.ResumeAsync(workflowContext, node, token);
        }
    }
}
