using System.Security.Claims;
using Juice.Workflows.Builder;
using Microsoft.AspNetCore.Http;

namespace Juice.Workflows.Services
{
    internal class WorkflowContextResolver : IWorkflowContextResolver
    {
        private IEnumerable<IWorkflowContextBuilder> _builders;
        private readonly IWorkflowRepository _workflowRepository;
        private readonly IWorkflowStateRepository? _workflowStateRepository;

        private readonly string? _user;

        public WorkflowContextResolver(IWorkflowRepository workflowRepository,
            IEnumerable<IWorkflowContextBuilder> builders,
            IWorkflowStateRepository? workflowStateRepository = default,
            IHttpContextAccessor? httpContextAccessor = default
            )
        {
            _workflowStateRepository = workflowStateRepository;
            _workflowRepository = workflowRepository;
            _builders = builders.OrderByDescending(b => b.Priority).ToArray();
            if (httpContextAccessor != null)
            {
                _user = httpContextAccessor?.HttpContext?.User?.FindFirst(ClaimTypes.Name)?.Value;
            }
        }
        public async Task<WorkflowContext?> ResolveAsync(string workflowId, CancellationToken token)
        {
            string defaultInstanceId = workflowId;
            foreach (var builder in _builders)
            {
                if (await builder.ExistsAsync(workflowId, token))
                {
                    var context = await builder.BuildAsync(workflowId, defaultInstanceId, token);
                    if (context != null)
                    {
                        return context;
                    }
                }
            }
            return default;
        }
        public async Task<WorkflowContext?> StateResolveAsync(string instanceId,
            Dictionary<string, object?>? input, CancellationToken token)
        {
            var workflowRecord = await _workflowRepository.GetAsync(instanceId, token);
            if (workflowRecord == null)
            {
                throw new Exception("Workflow not found");
            }
            if (_workflowStateRepository == null)
            {
                throw new InvalidOperationException("Unable to resolve service for type 'IWorkflowStateRepository'");
            }
            var defineId = workflowRecord.DefinitionId;
            foreach (var builder in _builders)
            {
                if (await builder.ExistsAsync(defineId, token))
                {
                    var context = await builder.BuildAsync(defineId, instanceId, token);
                    if (context != null)
                    {
                        var state = await _workflowStateRepository.GetAsync(instanceId, token);
                        return context.SetState(state).SetExecutionInfo(_user, workflowRecord, input);
                    }
                }
            }
            return default;
        }
    }
}
