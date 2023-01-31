using System.Collections.Concurrent;
using Juice.Services;

namespace Juice.Workflows.Builder
{

    public class WorkflowContextBuilder : WorkflowContextBuilderBase
    {
        public WorkflowContextBuilder(IStringIdGenerator stringIdGenerator,
            INodeLibrary nodeLibrary,
            IServiceProvider serviceProvider) : base(stringIdGenerator, nodeLibrary, serviceProvider)
        {

        }

        public WorkflowContext Build(WorkflowRecord workflow)
        {
            return new WorkflowContext(
                workflow.Id
                , workflow.Name ?? _name
                , _nodeRecords.Values.Select(n => new NodeContext(n, _nodes[n.Id], _properties.ContainsKey(n.Id) ? _properties[n.Id] : default)).ToList()
                , _flowRecords.Values.Select(f => new FlowContext(f, _flows[f.Id])).ToList()
                , _processRecords.Values
                , this.GetType().FullName
                );
        }

        protected override void AddNode(NodeRecord record, INode node)
        {
            base.AddNode(record, node);
            _currentNodeId = record.Id;
        }

        #region Workflow as code

        private string? _name;
        private string? _currentNodeId;
        private string? _currentProcessId;

        private WorkflowContextBuilder Append<T>(string? name = default, string? condition = default, bool isDefault = false)
    where T : class, INode
        {
            if (string.IsNullOrEmpty(_currentNodeId))
            {
                throw new InvalidOperationException("Workflow must has current node before");
            }

            if (typeof(T).IsAssignableTo(typeof(StartEvent)))
            {
                throw new ArgumentException("Start Event must be start of workflow");
            }

            var current = _nodes[_currentNodeId];
            if (current.GetType().IsAssignableTo(typeof(IGateway)))
            {
                if (!_gateways.TryPeek(out var gid) || gid != _currentNodeId)
                {
                    _gateways.Push(_currentNodeId);
                }
                if (current.GetType().IsAssignableTo(typeof(IEventBased))
                    && (
                        !typeof(T).IsAssignableTo(typeof(ICatching))
                        || !typeof(T).IsAssignableTo(typeof(IIntermediate))
                        )
                    )
                {
                    throw new ArgumentException("The next to Eventbased gateway must be intermediate catch event");
                }
            }
            else if (current.GetType().IsAssignableTo(typeof(IActivity)))
            {
                _branchActivities.Push(_currentNodeId);
            }

            var currentId = _currentNodeId;

            var id = typeof(T).IsAssignableTo(typeof(IGateway)) ? NewGatewayId()
                : typeof(T).IsAssignableTo(typeof(IEvent)) ? NewEventId()
                : NewActivityId();
            var flowId = NewFlowId();

            var node = _nodeLibrary.CreateInstance(typeof(T).Name, _serviceProvider);
            var record = new NodeRecord(id, name ?? node.DisplayText, _currentProcessId);

            AddNode(record, node);

            var flow = new SequenceFlow { };
            var flowRecord = new FlowRecord(flowId, currentId, id, default, _currentProcessId, condition);

            AddFlow(flowRecord, flow, isDefault);

            return this;
        }

        public WorkflowContextBuilder SetName(string name)
        {
            _name = name;
            return this;
        }

        public WorkflowContextBuilder SetProperties(Dictionary<string, object> properties)
        {
            if (string.IsNullOrEmpty(_currentNodeId))
            {
                throw new InvalidOperationException("Workflow must has current node before");
            }
            _properties[_currentNodeId] = properties;
            return this;
        }

        public WorkflowContextBuilder NewProcess(string? processId = default, string? name = default)
        {
            var process = new ProcessRecord(processId ?? NewProcessId(), name);
            _processRecords[process.Id] = process;
            _currentProcessId = process.Id;
            return this;
        }
        public WorkflowContextBuilder Start()
        {
            if (_currentProcessId == null)
            {
                NewProcess();
            }
            _currentNodeId = NewEventId();
            var record = new NodeRecord(_currentNodeId, "Start", _currentProcessId);
            AddNode(record, _nodeLibrary.CreateInstance(nameof(StartEvent), _serviceProvider));
            return this;
        }
        public WorkflowContextBuilder End()
            => Append<EndEvent>();
        public WorkflowContextBuilder Terminate()
           => Append<TerminateEvent>();

        public WorkflowContextBuilder Then<T>(string? name = default, string? condition = default, bool isDefault = false)
            where T : class, IActivity
            => Append<T>(name, condition, isDefault);

        public WorkflowContextBuilder Wait<T>(string? name = default, string? condition = default, bool isDefault = false)
            where T : class, IIntermediate
            => Append<T>(name, condition, isDefault);

        public WorkflowContextBuilder Attach<T>(string name)
            where T : class, IBoundary
        {
            if (string.IsNullOrEmpty(_currentNodeId))
            {
                throw new InvalidOperationException("Workflow must has current node before");
            }
            var id = NewEventId();
            var node = _nodeLibrary.CreateInstance(typeof(T).Name, _serviceProvider);
            var record = new NodeRecord(id, name ?? node.DisplayText).AttachTo(_currentNodeId);

            AddNode(record, node);

            return this;
        }

        private Stack<string> _branchActivities = new Stack<string>();
        private Stack<string> _gateways = new Stack<string>();

        public WorkflowContextBuilder Exclusive(string? name = default)
            => Gateway<ExclusiveGateway>(name);

        public WorkflowContextBuilder ExclusiveEventbased(string? name = default)
            => Gateway<EventBasedGateway>(name);

        public WorkflowContextBuilder Inclusive(string? name = default)
            => Gateway<InclusiveGateway>(name);

        public WorkflowContextBuilder Parallel(string? name = default)
            => Gateway<ParallelGateway>(name);

        private WorkflowContextBuilder Gateway<T>(string? name)
            where T : class, IGateway
        {
            if (string.IsNullOrEmpty(_currentNodeId))
            {
                throw new InvalidOperationException("Workflow must has current node before");
            }
            var current = _nodes[_currentNodeId];
            if (current.GetType().IsAssignableTo(typeof(IGateway)))
            {
                throw new InvalidOperationException("Gateway must has activity or event next to");
            }
            if (!string.IsNullOrEmpty(name) && _nodeRecords.Values.Any(n => n.Name == name))
            {
                throw new Exception($"Name {name} is existing");
            }
            Append<T>(name);
            _branchActivities.Clear();
            return this;
        }
        private WorkflowContextBuilder LastGateway()
        {
            if (_gateways.TryPop(out var id))
            {
                _currentNodeId = id;
                return this;
            }
            throw new InvalidOperationException("No more gateway to go back");
        }

        public WorkflowContextBuilder Fork()
        {

            if (string.IsNullOrEmpty(_currentNodeId))
            {
                throw new InvalidOperationException("Workflow must has current node before");
            }

            var current = _nodes[_currentNodeId];

            if (!current.GetType().IsAssignableTo(typeof(IGateway)))
            {
                if (!_gateways.Any())
                {
                    throw new InvalidOperationException("Could not fork because no ancestor gateway found");
                }
                return LastGateway();
            }
            return this;
        }

        public WorkflowContextBuilder Merge(params string[] names)
        {
            if (string.IsNullOrEmpty(_currentNodeId))
            {
                throw new InvalidOperationException("Workflow must has current node before");
            }

            var current = _nodes[_currentNodeId];

            if (!current.GetType().IsAssignableTo(typeof(IGateway)))
            {
                if (!_gateways.Any())
                {
                    throw new InvalidOperationException("Could not fork because no ancestor gateway found");
                }
                LastGateway();
            }

            var newCurrent = _nodes[_currentNodeId];
            var currentGatewayId = new string(_currentNodeId);

            var gatewayType = newCurrent.GetType();

            var leafNodes = names.Any() ? _nodeRecords.Values.Where(n => names.Contains(n.Name)).Select(n => n.Id)
                : GetLeafNodes(_nodeRecords[_currentNodeId]).Distinct();

            var id = NewGatewayId();
            var record = new NodeRecord { Id = id, Name = "Merge" };
            var mergeGateway = _nodeLibrary.CreateInstance(gatewayType.Name, _serviceProvider);
            AddNode(record, mergeGateway);

            foreach (var leaf in leafNodes)
            {
                var flowId = NewFlowId();
                var flowRecord = new FlowRecord(flowId, leaf, id);
                var flow = new SequenceFlow();
                AddFlow(flowRecord, flow);
            }

            if (current.GetType().IsAssignableTo(typeof(IGateway)))
            {
                var flowId = NewFlowId();
                var flowRecord = new FlowRecord(flowId, currentGatewayId, id);

                var flow = new SequenceFlow();
                AddFlow(flowRecord, flow, true);
            }

            return this;
        }

        public WorkflowContextBuilder Seek(string name)
        {
            _currentNodeId = _nodeRecords.Values.First(n => n.Name == name).Id;
            return this;
        }

        public WorkflowContextBuilder SubProcess(string name,
            Action<WorkflowContextBuilder> builder,
            string? condition,
            bool isDefault = false)
        {
            if (string.IsNullOrEmpty(_currentNodeId))
            {
                throw new InvalidOperationException("Workflow must has current node before");
            }

            var currentId = _currentNodeId;
            var id = NewActivityId();
            var flowId = NewFlowId();

            var node = _nodeLibrary.CreateInstance(typeof(SubProcess).Name, _serviceProvider);
            var record = new NodeRecord { Id = id, Name = name ?? node.DisplayText };

            AddNode(record, node);

            var flow = new SequenceFlow { };
            var flowRecord = new FlowRecord(flowId, currentId, id, default, default, condition);

            AddFlow(flowRecord, flow, isDefault);

            var tmpWorkflowId = _idGenerator.GenerateRandomId(6);

            var contextBuilder = new WorkflowContextBuilder(_idGenerator, _nodeLibrary, _serviceProvider)
                .NewProcess(record.Id, record.Name);
            builder(contextBuilder);

            string? nullCorrelationId = default;
            string? nullName = default;

            var context = contextBuilder.Build(new WorkflowRecord(tmpWorkflowId, tmpWorkflowId, nullCorrelationId, nullName));

            foreach (var n in context.Nodes.Values)
            {
                _nodes[n.Record.Id] = n.Node;
                _nodeRecords[n.Record.Id] = n.Record;
            }

            foreach (var f in context.Flows)
            {
                _flows[f.Record.Id] = f.Flow;
                _flowRecords[f.Record.Id] = f.Record;
            }
            return this;
        }

        private List<string> GetLeafNodes(string flowId)
        {
            var flow = _flowRecords[flowId];
            var destNode = _nodeRecords[flow.DestinationRef];
            return GetLeafNodes(destNode);
        }

        private List<string> GetLeafNodes(NodeRecord node)
        {
            var leafNodes = new List<string>();
            var branches = node.Outgoings;
            if (branches.Any())
            {
                foreach (var branch in branches)
                {
                    leafNodes.AddRange(GetLeafNodes(branch));
                }
            }
            else
            {
                leafNodes.Add(node.Id);
            }
            return leafNodes;
        }
        #endregion

    }


    // Manage static incode workflow
    internal class IncodeWorkflowContextBuilder : IWorkflowContextBuilder
    {
        private static ConcurrentDictionary<string, WorkflowContextBuilder> _store
            = new ConcurrentDictionary<string, WorkflowContextBuilder>();

        public int Priority => 99;

        public async Task<WorkflowContext> BuildAsync(string workflowId,
            string instanceId,
            CancellationToken token)
        {
            return _store[workflowId].Build(new WorkflowRecord(instanceId, workflowId, default, default));
        }

        public Task<bool> ExistsAsync(string workflowId, CancellationToken token)
            => Task.FromResult(_store.ContainsKey(workflowId));

        public void Register(string workflowId, WorkflowContextBuilder builder)
        {
            _store[workflowId] = builder;
        }
    }
}
