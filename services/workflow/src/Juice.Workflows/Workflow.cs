using Juice.Services;
using Juice.Workflows.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace Juice.Workflows
{
    internal class Workflow : IWorkflow
    {
        public WorkflowContext? ExecutedContext
        {
            get
            {
                return _context;
            }
        }

        private IEnumerable<IWorkflowContextBuilder> _builders;
        private WorkflowContext? _context;

        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IWorkflowStateReposistory _stateReposistory;
        private readonly IWorkflowRepository _workflowRepository;

        private readonly IStringIdGenerator _idGenerator;

        public Workflow(IServiceScopeFactory scopeFactory,
            IWorkflowStateReposistory stateReposistory,
            IStringIdGenerator idGenerator,
            IWorkflowRepository workflowRepository,
            IEnumerable<IWorkflowContextBuilder> builders)
        {
            _scopeFactory = scopeFactory;
            _builders = builders;
            _idGenerator = idGenerator;
            _stateReposistory = stateReposistory;
            _workflowRepository = workflowRepository;
        }

        public async Task<OperationResult<WorkflowExecutionResult>> StartAsync(string workflowId, string? correlationId, CancellationToken token = default)
        {
            try
            {
                var id = _idGenerator.GenerateUniqueId();

                var createResult = await _workflowRepository.CreateAsync(new WorkflowRecord
                {
                    Id = id,
                    RefWorkflowId = workflowId,
                    CorrelationId = correlationId,
                    Status = WorkflowStatus.Idle
                }, token);

                if (!createResult.Succeeded)
                {
                    return OperationResult.Failed<WorkflowExecutionResult>(createResult.Exception, "Cannot start new workflow. " + (createResult.Message ?? ""));
                }

                foreach (var builder in _builders)
                {
                    if (await builder.ExistsAsync(workflowId, token))
                    {
                        var context = await builder.BuildAsync(workflowId, id, token);
                        if (context != null)
                        {
                            return await ExecuteAsync(context, default, token);
                        }
                    }
                }
                return OperationResult.Failed<WorkflowExecutionResult>($"Cannot resolve workflow context to execute {workflowId}");
            }
            catch (Exception ex)
            {
                return OperationResult.Failed<WorkflowExecutionResult>(ex);
            }
        }

        public async Task<OperationResult<WorkflowExecutionResult>> ResumeAsync(string workflowId, string nodeId, CancellationToken token = default)
        {
            if (workflowId == null)
            {
                return OperationResult.Failed<WorkflowExecutionResult>(
                    new ArgumentNullException(nameof(workflowId)));
            }
            if (nodeId == null)
            {
                return OperationResult.Failed<WorkflowExecutionResult>(
                    new ArgumentNullException(nameof(nodeId)));
            }
            var workflowRecord = await _workflowRepository.GetAsync(workflowId, token);
            if (workflowRecord == null)
            {
                return OperationResult.Failed<WorkflowExecutionResult>("Workflow not found");
            }
            var defineId = workflowRecord.RefWorkflowId ?? workflowRecord.Id;
            foreach (var builder in _builders)
            {
                if (await builder.ExistsAsync(defineId, token))
                {
                    var context = await builder.BuildAsync(defineId, workflowId, token);
                    if (context != null)
                    {
                        return await ExecuteAsync(context, nodeId, token);
                    }
                }
            }
            return OperationResult.Failed<WorkflowExecutionResult>($"Cannot resolve workflow context to execute {workflowId}");
        }

        private async Task<OperationResult<WorkflowExecutionResult>> ExecuteAsync(WorkflowContext context, string? nodeId = default, CancellationToken token = default)
        {
            _context = context;
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var executor = scope.ServiceProvider.GetRequiredService<WorkflowExecutor>();
                var rs = await executor.ExecuteAsync(_context, nodeId, token);

                var state = rs.Context.State;

                await _stateReposistory.PersistAsync(context.WorkflowId, state, token);

                return OperationResult.Result(rs);
            }
            catch (Exception ex)
            {
                return OperationResult.Failed<WorkflowExecutionResult>(ex);
            }

        }
    }
}
