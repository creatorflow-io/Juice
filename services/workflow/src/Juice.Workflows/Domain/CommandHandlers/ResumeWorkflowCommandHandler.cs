using Juice.Workflows.Domain.Commands;

namespace Juice.Workflows.Domain.CommandHandlers
{
    public class ResumeWorkflowCommandHandler : IRequestHandler<ResumeWorkflowCommand, IOperationResult<WorkflowExecutionResult>>
    {
        private readonly IWorkflow _workflow;
        public ResumeWorkflowCommandHandler(IWorkflow workflow)
        {
            _workflow = workflow;
        }
        public async Task<IOperationResult<WorkflowExecutionResult>> Handle(ResumeWorkflowCommand request, CancellationToken cancellationToken)
        {
            return await _workflow.ResumeAsync(request.WorkflowId, request.NodeId, request.Parameters);
        }
    }
}
