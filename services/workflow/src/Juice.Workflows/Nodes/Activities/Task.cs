namespace Juice.Workflows.Nodes.Activities
{
    //public class Task : Activity
    //{
    //    public override LocalizedString DisplayText => Localizer["Task"];

    //    public Task(IServiceProvider serviceProvider, IStringLocalizerFactory stringLocalizer)
    //        : base(serviceProvider, stringLocalizer)
    //    {
    //    }

    //    public override async Task<NodeExecutionResult> StartAsync(WorkflowContext workflowContext, NodeContext node, FlowContext? flow, CancellationToken token)
    //    {
    //        try
    //        {
    //            using var scope = _serviceProvider.CreateScope();
    //            var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
    //            var rs = await mediator.Send(new StartTaskCommand(workflowContext.WorkflowId, workflowContext.CorrelationId, node));
    //            if (rs.Succeeded)
    //            {
    //                workflowContext.AddDomainEvent(new TaskRequestDomainEvent(node));

    //                return await base.StartAsync(workflowContext, node, flow, token);
    //            }
    //            return Fault(rs.Message ?? "Failed to start a user task");
    //        }
    //        catch (Exception ex)
    //        {
    //            return Fault(ex.Message);
    //        }
    //    }

    //    public override Task<NodeExecutionResult> ResumeAsync(WorkflowContext workflowContext, NodeContext node, CancellationToken token)
    //    {
    //        workflowContext.AddDomainEvent(new TaskCompleteDomainEvent(node));
    //        return base.ResumeAsync(workflowContext, node, token);
    //    }
    //}
}
