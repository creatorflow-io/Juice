
namespace Juice.Workflows.Builder
{
    public class DbWorkflowContextBuilder : IWorkflowContextBuilder
    {
        public int Priority => 98;

        private IDefinitionRepository _definitionReposistory;
        private IWorkflowStateRepository _stateReposistory;
        private IWorkflowRepository _workflowRepository;
        private INodeLibrary _nodeLibrary;
        private IServiceProvider _serviceProvider;

        public DbWorkflowContextBuilder(IDefinitionRepository definitionReposistory, IWorkflowStateRepository stateReposistory, IWorkflowRepository workflowRepository, INodeLibrary nodeLibrary, IServiceProvider serviceProvider)
        {
            _definitionReposistory = definitionReposistory;
            _stateReposistory = stateReposistory;
            _workflowRepository = workflowRepository;
            _nodeLibrary = nodeLibrary;
            _serviceProvider = serviceProvider;
        }

        public async Task<WorkflowContext> BuildAsync(string workflowId, string instanceId,
            Dictionary<string, object?>? input, CancellationToken token)
        {
            var definition = await _definitionReposistory.GetAsync(workflowId, token);
            if (definition == null)
            {
                throw new Exception("Workflow definition not found");
            }

            var state = await _stateReposistory.GetAsync(instanceId, token);

            var workflow = await _workflowRepository.GetAsync(instanceId, token);
            if (workflow == null)
            {
                throw new Exception("Workflow not found");
            }
            var user = default(string?);
            var (processes, nodes, flows) = definition.GetData();
            var nodesContext = new List<NodeContext>();
            var flowsContext = new List<FlowContext>();
            foreach (var node in nodes)
            {
                var nodeImpl = _nodeLibrary.CreateInstance(node.TypeName, _serviceProvider);
                nodesContext.Add(new NodeContext(node.NodeRecord, nodeImpl));
            }
            foreach (var flow in flows)
            {
                var flowImpl = new SequenceFlow();
                flowsContext.Add(new FlowContext(flow.FlowRecord, flowImpl));
            }

            return new WorkflowContext(workflow.Id
               , workflow.CorrelationId
               , state?.NodeSnapshots
               , state?.FlowSnapshots
               , state?.ProcessSnapshots
               , input
               , state?.Output
               , nodesContext
               , flowsContext
               , processes
               , definition.Name
               , user
               , this.GetType().FullName
               );
        }

        public Task<bool> ExistsAsync(string workflowId, CancellationToken token)
            => _definitionReposistory.ExistAsync(workflowId, token);
    }
}
