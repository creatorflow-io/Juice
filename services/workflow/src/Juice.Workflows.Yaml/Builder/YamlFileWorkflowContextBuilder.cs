using Juice.Workflows.Builder;
using Juice.Workflows.Domain.AggregatesModel.WorkflowAggregate;
using Juice.Workflows.Execution;


namespace Juice.Workflows.Yaml.Builder
{
    internal class YamlFileWorkflowContextBuilder : IWorkflowContextBuilder
    {
        public int Priority => 1;
        private string _directory = "workflows";

        private WorkflowContextBuilder _builder;

        private bool _build = true;

        public YamlFileWorkflowContextBuilder(
            WorkflowContextBuilder builder
        )
        {
            _builder = builder;
        }

        public async Task<WorkflowContext> BuildAsync(string workflowId,
            string instanceId,
            CancellationToken token)
        {
            var file = Path.Combine(_directory, workflowId + ".yaml");

            if (_build)
            {
                _build = false;
                var yml = await File.ReadAllTextAsync(file);
                return _builder.Build(yml, new WorkflowRecord(instanceId, workflowId, default, default), true);

            }
            var nullYml = default(string?);
            return _builder.Build(nullYml, new WorkflowRecord(instanceId, workflowId, default, default), false);
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
