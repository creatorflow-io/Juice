using Juice.Services;

namespace Juice.Workflows.Builder
{
    // Build the simple workflow for logic testing

    public abstract class WorkflowContextBuilderBase
    {
        protected Dictionary<string, NodeRecord> _nodeRecords = new Dictionary<string, NodeRecord>();
        protected Dictionary<string, INode> _nodes = new Dictionary<string, INode>();
        protected Dictionary<string, FlowRecord> _flowRecords = new Dictionary<string, FlowRecord>();
        protected Dictionary<string, IFlow> _flows = new Dictionary<string, IFlow>();

        protected Dictionary<string, ProcessRecord> _processRecords = new Dictionary<string, ProcessRecord>();

        protected Dictionary<string, Dictionary<string, object>> _properties = new Dictionary<string, Dictionary<string, object>>();

        protected IStringIdGenerator _idGenerator;
        protected INodeLibrary _nodeLibrary;
        protected IServiceProvider _serviceProvider;
        protected bool _needBuild = true;
        public WorkflowContextBuilderBase(
           IStringIdGenerator stringIdGenerator,
           INodeLibrary nodeLibrary,
           IServiceProvider serviceProvider
       )
        {
            _idGenerator = stringIdGenerator;
            _nodeLibrary = nodeLibrary;
            _serviceProvider = serviceProvider;
        }


        #region Builder
        protected string NewProcessId() => "Process_" + _idGenerator.GenerateRandomId(6);
        protected string NewEventId() => "Event_" + _idGenerator.GenerateRandomId(6);
        protected string NewGatewayId() => "Gateway_" + _idGenerator.GenerateRandomId(6);
        protected string NewActivityId() => "Activity_" + _idGenerator.GenerateRandomId(6);
        protected string NewFlowId() => "Flow_" + _idGenerator.GenerateRandomId(6);

        protected virtual void AddNode(NodeRecord record, INode node)
        {
            if (node == null)
            {
                throw new ArgumentNullException(nameof(node));
            }
            if (_nodes.ContainsKey(record.Id))
            {
                throw new ArgumentException($"Node {record.Id} is already exists.");
            }
            _nodes[record.Id] = node;
            _nodeRecords[record.Id] = record;
        }

        protected virtual void AddFlow(FlowRecord record, IFlow flow, bool isDefault = false)
        {
            if (flow == null)
            {
                throw new ArgumentNullException(nameof(flow));
            }
            if (_flows.ContainsKey(record.Id))
            {
                throw new ArgumentException($"Flow {record.Id} is already exists.");
            }

            if (!_nodeRecords.ContainsKey(record.SourceRef))
            {
                throw new ArgumentException($"Source node {record.SourceRef} does not exist.");
            }

            if (!_nodeRecords.ContainsKey(record.DestinationRef))
            {
                throw new ArgumentException($"Destination node {record.DestinationRef} does not exist.");
            }

            _flows[record.Id] = flow;
            _flowRecords[record.Id] = record;

            var source = _nodeRecords[record.SourceRef];
            source.AddOutgoing(record.Id);

            if (isDefault)
            {
                source.SetDefault(record.Id);
            }

            var dest = _nodeRecords[record.DestinationRef];
            dest.AddIncoming(record.Id);
        }

        protected (INode Node, NodeRecord Record) CreateNode(string type, string? name, string processId)
        {
            var node = _nodeLibrary.CreateInstance(type, _serviceProvider);
            var nodeId = node is IGateway ? NewGatewayId()
            : node is IEvent ? NewEventId()
            : NewActivityId();
            var record = new NodeRecord { Id = nodeId, Name = name ?? node.DisplayText, ProcessIdRef = processId };
            return (node, record);
        }

        protected virtual void AddProcess(ProcessRecord process)
        {
            _processRecords[process.Id] = process;
        }
        #endregion
    }

}
