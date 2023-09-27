using Juice.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Juice.Workflows
{
    internal class Workflow : IWorkflow
    {
        public WorkflowContext? ExecutedContext
        {
            get
            {
                return _workflowContextAccessor.Context;
            }
            private set
            {
                _workflowContextAccessor.SetContext(value);
            }
        }

        private ILogger _logger;

        private readonly IServiceProvider _serviceProvider;
        private readonly IWorkflowStateRepository _stateReposistory;
        private readonly IWorkflowRepository _workflowRepository;

        private readonly IStringIdGenerator _idGenerator;

        private readonly IWorkflowContextAccessor _workflowContextAccessor;
        private readonly IWorkflowContextResolver _workflowContextResolver;

        public Workflow(IServiceProvider serviceProvider,
            ILogger<Workflow> logger,
            IWorkflowStateRepository stateReposistory,
            IStringIdGenerator idGenerator,
            IWorkflowRepository workflowRepository,
            IWorkflowContextAccessor workflowContextAccessor,
            IWorkflowContextResolver workflowContextResolver)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
            _idGenerator = idGenerator;
            _stateReposistory = stateReposistory;
            _workflowRepository = workflowRepository;
            _workflowContextAccessor = workflowContextAccessor;
            _workflowContextResolver = workflowContextResolver;
        }

        public async Task<OperationResult<WorkflowExecutionResult>> StartAsync(string workflowId,
            string? correlationId, string? name, Dictionary<string, object?>? input,
            CancellationToken token = default)
        {
            try
            {
                var id = _idGenerator.GenerateUniqueId();

                var workflow = new WorkflowRecord(id, workflowId, correlationId, name);
                var createResult = await _workflowRepository.CreateAsync(workflow, token);

                if (!createResult.Succeeded)
                {
                    return OperationResult.Failed<WorkflowExecutionResult>(createResult.Exception, "Cannot start new workflow. " + (createResult.Message ?? ""));
                }

                _workflowContextAccessor.SetWorkflowId(id);

                var context = await _workflowContextResolver.StateResolveAsync(id, input, token);
                if (context != null)
                {
                    ExecutedContext = context;

                    _logger.LogInformation("WorkflowContext resolved by {resolver}", context.ResolvedBy);
                    return await ExecuteAsync(context, default, token);
                }

                return OperationResult.Failed<WorkflowExecutionResult>($"Cannot resolve workflow context to execute {workflowId}");
            }
            catch (Exception ex)
            {
                return OperationResult.Failed<WorkflowExecutionResult>(ex);
            }
        }

        public async Task<OperationResult<WorkflowExecutionResult>> ResumeAsync(string workflowId,
            string nodeId, Dictionary<string, object?>? input,
            CancellationToken token = default)
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

            _workflowContextAccessor.SetWorkflowId(workflowId);
            try
            {
                var context = ExecutedContext
                    ?? await _workflowContextResolver.StateResolveAsync(workflowId, input, token);
                if (context != null)
                {
                    ExecutedContext = context;

                    _logger.LogInformation("WorkflowContext resolved by {resolver}", context.ResolvedBy);
                    return await ExecuteAsync(context, nodeId, token);
                }
            }
            catch (Exception ex)
            {
                return OperationResult.Failed<WorkflowExecutionResult>(ex);
            }
            return OperationResult.Failed<WorkflowExecutionResult>($"Cannot resolve workflow context to execute {workflowId}");
        }

        private async Task<OperationResult<WorkflowExecutionResult>> ExecuteAsync(
            WorkflowContext context, string? nodeId = default, CancellationToken token = default)
        {
            try
            {
                if (context.WorkflowRecord != null)
                {
                    context.WorkflowRecord.UpdateStatus(WorkflowStatus.Executing, default);
                    await _workflowRepository.UpdateAsync(context.WorkflowRecord, token);
                }

                var executor = _serviceProvider.GetRequiredService<WorkflowExecutor>();

                var rs = await executor.ExecuteAsync(ExecutedContext!, nodeId, token);

                var state = rs.Context.State;

                await _stateReposistory.PersistAsync(context.WorkflowId, state, token);

                if (context.WorkflowRecord != null)
                {
                    context.WorkflowRecord.UpdateStatus(rs.Status, default);
                    await _workflowRepository.UpdateAsync(context.WorkflowRecord, token);
                }

                return OperationResult.Result(rs);
            }
            catch (Exception ex)
            {
                return OperationResult.Failed<WorkflowExecutionResult>(ex);
            }

        }
    }
}
