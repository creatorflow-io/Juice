
namespace Juice.Workflows.Builder
{
    public class DbWorkflowContextBuilder : IWorkflowContextBuilder
    {
        public int Priority => 98;

        private IDefinitionRepository _definitionReposistory;
        private INodeLibrary _nodeLibrary;
        private IServiceProvider _serviceProvider;

        public DbWorkflowContextBuilder(IDefinitionRepository definitionReposistory,
            INodeLibrary nodeLibrary, IServiceProvider serviceProvider)
        {
            _definitionReposistory = definitionReposistory;
            _nodeLibrary = nodeLibrary;
            _serviceProvider = serviceProvider;
        }

        public async Task<WorkflowContext> BuildAsync(string workflowId, string instanceId,
            CancellationToken token)
        {
            var definition = await _definitionReposistory.GetAsync(workflowId, token);
            if (definition == null)
            {
                throw new Exception("Workflow definition not found");
            }

            var (processes, nodes, flows) = definition.GetData();
            var nodesContext = new List<NodeContext>();
            var flowsContext = new List<FlowContext>();
            foreach (var node in nodes)
            {
                var nodeImpl = _nodeLibrary.CreateInstance(node.TypeName, _serviceProvider);
                nodesContext.Add(new NodeContext(node.NodeRecord, nodeImpl, node.Properties));
            }
            foreach (var flow in flows)
            {
                var flowImpl = new SequenceFlow();
                flowsContext.Add(new FlowContext(flow.FlowRecord, flowImpl));
            }

            return new WorkflowContext(instanceId
               , definition.Name
               , nodesContext
               , flowsContext
               , processes
               , this.GetType().FullName
               );
        }

        public Task<bool> ExistsAsync(string workflowId, CancellationToken token)
            => _definitionReposistory.ExistAsync(workflowId, token);
    }
}
