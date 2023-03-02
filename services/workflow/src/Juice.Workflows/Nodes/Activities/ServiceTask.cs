using Juice.Extensions;
using Juice.Workflows.Domain.Commands;
using Microsoft.Extensions.DependencyInjection;

namespace Juice.Workflows.Nodes.Activities
{
    public class ServiceTask : Activity
    {
        public override LocalizedString DisplayText => Localizer["Service Task"];

        public ServiceTask(IServiceProvider serviceProvider, IStringLocalizerFactory stringLocalizer)
            : base(serviceProvider, stringLocalizer)
        {
        }

        public override async Task<NodeExecutionResult> StartAsync(WorkflowContext workflowContext, NodeContext node, FlowContext? flow, CancellationToken token)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
                var rs = await mediator.Send(new StartTaskCommand<ServiceTask>(workflowContext.WorkflowId, workflowContext.CorrelationId, node));
                if (rs.Succeeded)
                {
                    workflowContext.AddDomainEvent(new ServiceTaskRequestDomainEvent(node));
                    return await base.StartAsync(workflowContext, node, flow, token);
                }
                return Fault(rs.Message ?? "Failed to start a service task");
            }
            catch (Exception ex)
            {
                return Fault(ex.Message);
            }
        }

        public override Task<NodeExecutionResult> ResumeAsync(WorkflowContext workflowContext, NodeContext node, CancellationToken token)
        {
            var status = workflowContext.Input.GetOption<WorkflowStatus?>("TaskStatus") ?? WorkflowStatus.Finished;
            var message = workflowContext.Input.GetOption<string?>("TaskMessage");

            if (status == WorkflowStatus.Faulted || status == WorkflowStatus.Aborted)
            {
                return Task.FromResult(Fault(message ?? $"Service responsed \"{status.DisplayValue()}\" status."));
            }
            else if (status == WorkflowStatus.Executing)
            {
                return Task.FromResult(Executing(message));
            }

            workflowContext.AddDomainEvent(new ServiceTaskCompleteDomainEvent(node));
            return Task.FromResult(string.IsNullOrEmpty(message) ? Outcomes("Done") : new NodeExecutionResult(message, WorkflowStatus.Finished, new string[] { "Done" }));
        }
    }
}
