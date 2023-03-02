using Juice.Workflows.Domain.Commands;
using Microsoft.Extensions.DependencyInjection;

namespace Juice.Workflows.Nodes.Activities
{
    public class SendTask : Activity
    {
        public SendTask(IServiceProvider serviceProvider, IStringLocalizerFactory stringLocalizer)
            : base(serviceProvider, stringLocalizer)
        {
        }

        public override LocalizedString DisplayText => Localizer["Send Task"];

        public override async Task<NodeExecutionResult> StartAsync(WorkflowContext workflowContext, NodeContext node, FlowContext? flow, CancellationToken token)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
                var rs = await mediator.Send(new StartTaskCommand<SendTask>(workflowContext.WorkflowId, workflowContext.CorrelationId, node));
                if (rs.Succeeded)
                {
                    return Outcomes("Sent");
                }
                return Fault(rs.Message ?? "Failed to start a send task");
            }
            catch (Exception ex)
            {
                return Fault(ex.Message);
            }
        }

        public override Task<NodeExecutionResult> ResumeAsync(WorkflowContext workflowContext, NodeContext node, CancellationToken token) => throw new NotImplementedException();

        public override IEnumerable<Outcome> GetPossibleOutcomes(WorkflowContext workflowContext, NodeContext node) => new Outcome[] { new Outcome(Localizer["Sent"]) };
    }
}
