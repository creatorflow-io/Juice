using Juice.Workflows.Builder;
using Juice.Workflows.Domain.AggregatesModel.WorkflowAggregate;
using Juice.Workflows.Domain.AggregatesModel.WorkflowStateAggregate;
using Juice.Workflows.Execution;


namespace Juice.Workflows.Yaml.Builder
{
    internal class YamlFileWorkflowContextBuilder : IWorkflowContextBuilder
    {
        private string _directory = "workflows";

        private IWorkflowStateReposistory _stateReposistory;
        private IWorkflowRepository _workflowRepository;

        private WorkflowContextBuilder _builder;

        private bool _build = true;

        public YamlFileWorkflowContextBuilder(
            IWorkflowStateReposistory stateReposistory,
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
            var file = Path.Combine(_directory, workflowId + ".yaml");

            var state = await _stateReposistory.GetAsync(instanceId ?? workflowId, token);

            var workflow = await _workflowRepository.GetAsync(instanceId ?? workflowId, token);
            if (workflow == null)
            {
                throw new Exception("Workflow not found");
            }
            if (_build)
            {
                _build = false;
                var yml = await File.ReadAllTextAsync(file);
                return _builder.Build(yml, workflow, state, user, input, true);

            }
            var nullYml = default(string?);
            return _builder.Build(nullYml, workflow, state, user, input, false);
        }

        public Task<bool> ExistsAsync(string workflowId, CancellationToken token)
            => Task.FromResult(File.Exists(Path.Combine(_directory, workflowId + ".yaml")));

        public void SetWorkflowsDirectory(string? directory)
        {
            if (!string.IsNullOrWhiteSpace(directory))
            {
                _directory = directory;
            }
        }

    }
}
