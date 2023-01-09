using Juice.Workflows.Builder;
using Juice.Workflows.Domain.AggregatesModel.WorkflowAggregate;
using Juice.Workflows.Execution;

namespace Juice.Workflows.Bpmn.Builder
{
    internal class BpmnFileWorkflowContextBuilder : IWorkflowContextBuilder
    {
        public int Priority => 1;
        private string _directory = "workflows";

        private WorkflowContextBuilder _builder;

        private bool _build = true;

        public BpmnFileWorkflowContextBuilder(
            WorkflowContextBuilder builder
        )
        {
            _builder = builder;
        }

        public async Task<WorkflowContext> BuildAsync(string workflowId,
            string instanceId,
            CancellationToken token)
        {
            var file = Path.Combine(_directory, workflowId + ".bpmn");

            if (_build)
            {
                _build = false;
                using var stream = File.OpenRead(file);
                using var reader = new StreamReader(stream);
                return _builder.Build(reader, new WorkflowRecord(instanceId, workflowId, default, default), true);

            }
            var nullTextReader = default(TextReader?);
            return _builder.Build(nullTextReader, new WorkflowRecord(instanceId, workflowId, default, default), false);
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
