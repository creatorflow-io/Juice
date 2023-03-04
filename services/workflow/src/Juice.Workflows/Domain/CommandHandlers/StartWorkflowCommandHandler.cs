using Juice.Workflows.Domain.Commands;

namespace Juice.Workflows.Domain.CommandHandlers
{
    public class StartWorkflowCommandHandler : IRequestHandler<StartWorkflowCommand, IOperationResult>
    {
        private readonly IWorkflow _workflow;
        public StartWorkflowCommandHandler(IWorkflow workflow)
        {
            _workflow = workflow;
        }
        public async Task<IOperationResult> Handle(StartWorkflowCommand request, CancellationToken cancellationToken)
        {
            return await _workflow.StartAsync(request.WorkflowId, request.CorrelationId, request.Name, request.Parameters);
        }
    }
}
