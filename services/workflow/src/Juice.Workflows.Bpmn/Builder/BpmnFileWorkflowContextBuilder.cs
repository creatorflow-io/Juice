using Juice.Workflows.Builder;
using Juice.Workflows.Domain.AggregatesModel.WorkflowAggregate;
using Juice.Workflows.Domain.AggregatesModel.WorkflowStateAggregate;
using Juice.Workflows.Execution;

namespace Juice.Workflows.Bpmn.Builder
{
    internal class BpmnFileWorkflowContextBuilder : IWorkflowContextBuilder
    {
        public int Priority => 1;
        private string _directory = "workflows";

        private IWorkflowStateRepository _stateReposistory;
        private IWorkflowRepository _workflowRepository;

        private WorkflowContextBuilder _builder;

        private bool _build = true;

        public BpmnFileWorkflowContextBuilder(
            IWorkflowStateRepository stateReposistory,
            WorkflowContextBuilder builder,
            IWorkflowRepository workflowRepository
        )
        {
            _stateReposistory = stateReposistory;
            _workflowRepository = workflowRepository;
            _builder = builder;
        }

        public async Task<WorkflowContext> BuildAsync(string workflowId,
            string instanceId, Dictionary<string, object?>? input,
            CancellationToken token)
        {
            var user = default(string?);
            var file = Path.Combine(_directory, workflowId + ".bpmn");

            var state = await _stateReposistory.GetAsync(instanceId ?? workflowId, token);

            var workflow = await _workflowRepository.GetAsync(instanceId ?? workflowId, token);
            if (workflow == null)
            {
                throw new Exception("Workflow not found");
            }
            if (_build)
            {
                _build = false;
                using var stream = File.OpenRead(file);
                using var reader = new StreamReader(stream);
                return _builder.Build(reader, workflow, state, user, input, true);

            }
            var nullTextReader = default(TextReader?);
            return _builder.Build(nullTextReader, workflow, state, user, input, false);
        }

        public Task<bool> ExistsAsync(string workflowId, CancellationToken token)
            => Task.FromResult(File.Exists(Path.Combine(_directory, workflowId + ".bpmn")));

        public void SetWorkflowsDirectory(string? directory)
        {
            if (!string.IsNullOrWhiteSpace(directory))
            {
                _directory = directory;
            }
        }

    }
}
