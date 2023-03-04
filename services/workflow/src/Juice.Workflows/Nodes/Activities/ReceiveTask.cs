using Juice.Workflows.Domain.Commands;
using Microsoft.Extensions.DependencyInjection;

namespace Juice.Workflows.Nodes.Activities
{
    public class ReceiveTask : Activity
    {
        public ReceiveTask(IServiceProvider serviceProvider,
            IStringLocalizerFactory stringLocalizer)
            : base(serviceProvider, stringLocalizer)
        {
        }

        public override LocalizedString DisplayText => Localizer["Receive Task"];

        public override async Task<NodeExecutionResult> StartAsync(WorkflowContext workflowContext, NodeContext node, FlowContext? flow, CancellationToken token)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
                var rs = await mediator.Send(new StartTaskCommand<ReceiveTask>(workflowContext.WorkflowId, workflowContext.CorrelationId, node));
                if (rs.Succeeded)
                {
                    return Halt();
                }
                return Fault(rs.Message ?? "Failed to start a receive task");
            }
            catch (Exception ex)
            {
                return Fault(ex.Message);
            }
        }

        public override Task<NodeExecutionResult> ResumeAsync(WorkflowContext workflowContext, NodeContext node, CancellationToken token)
            => Task.FromResult(Outcomes("Received"));

        public override IEnumerable<Outcome> GetPossibleOutcomes(WorkflowContext workflowContext, NodeContext node) => new Outcome[] { new Outcome(Localizer["Received"]) };
    }
}
