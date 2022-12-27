using Juice.Services;
using Juice.Workflows.Builder;
using Juice.Workflows.Domain.AggregatesModel.WorkflowAggregate;
using Juice.Workflows.Domain.AggregatesModel.WorkflowStateAggregate;
using Juice.Workflows.Execution;
using Juice.Workflows.Models;
using Juice.Workflows.Nodes.Activities;
using Juice.Workflows.Services;
using Juice.Workflows.Yaml.Models;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Juice.Workflows.Yaml.Builder
{
    internal class YamlWorkflowContextBuilder : IWorkflowContextBuilder
    {
        private string _directory = "workflows";
        private Dictionary<string, NodeRecord> _nodeRecords = new Dictionary<string, NodeRecord>();
        private Dictionary<string, INode> _nodes = new Dictionary<string, INode>();
        private Dictionary<string, FlowRecord> _flowRecords = new Dictionary<string, FlowRecord>();
        private Dictionary<string, IFlow> _flows = new Dictionary<string, IFlow>();

        private IStringIdGenerator _idGenerator;
        private INodeLibrary _nodeLibrary;
        private IServiceProvider _serviceProvider;
        private IWorkflowStateReposistory _stateReposistory;

        private bool _needBuild = true;

        public YamlWorkflowContextBuilder(
            IWorkflowStateReposistory stateReposistory,
             IStringIdGenerator stringIdGenerator,
           INodeLibrary nodeLibrary,
           IServiceProvider serviceProvider
        )
        {
            _stateReposistory = stateReposistory;
            _idGenerator = stringIdGenerator;
            _nodeLibrary = nodeLibrary;
            _serviceProvider = serviceProvider;
        }

        public async Task<WorkflowContext> BuildAsync(string workflowId,
            string? instanceId, Dictionary<string, object?>? input,
            CancellationToken token)
        {
            var file = Path.Combine(_directory, workflowId + ".yaml");

            var state = await _stateReposistory.GetAsync(instanceId ?? workflowId, token);
            if (_needBuild)
            {
                var deserializer = new DeserializerBuilder()
                .WithNamingConvention(UnderscoredNamingConvention.Instance)  // see height_in_inches in sample yml 
                .Build();

                var yml = await File.ReadAllTextAsync(file);

                var process = deserializer.Deserialize<Process>(yml);

                BuildProcess(process);
                _needBuild = false;
            }
            return new WorkflowContext(instanceId ?? workflowId
                , default
                , state?.NodeSnapshots
                , state?.FlowSnapshots
                , input
                , state?.Output
                , _nodeRecords.Values.Select(n => new NodeContext(n, _nodes[n.Id])).ToList()
                , _flowRecords.Values.Select(f => new FlowContext(f, _flows[f.Id])).ToList()
                , default
                , instanceId
                );
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


        #region Builder
        private string NewEventId() => "Event_" + _idGenerator.GenerateRandomId(6);
        private string NewGatewayId() => "Gateway_" + _idGenerator.GenerateRandomId(6);
        private string NewActivityId() => "Activity_" + _idGenerator.GenerateRandomId(6);
        private string NewFlowId() => "Flow_" + _idGenerator.GenerateRandomId(6);

        private void AddNode(NodeRecord record, INode node)
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

        private void AddFlow(FlowRecord record, IFlow flow, bool isDefault = false)
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
            if (!source.Outgoings.Contains(record.Id))
            {
                source.Outgoings = source.Outgoings.Append(record.Id).ToArray();
            }
            if (isDefault)
            {
                source.Default = record.Id;
            }

            var dest = _nodeRecords[record.DestinationRef];
            if (!dest.Incomings.Contains(record.Id))
            {
                dest.Incomings = dest.Incomings.Append(record.Id).ToArray();
            }
        }

        private (INode Node, NodeRecord Record) CreateNode(string type, string? name, string? ownerId)
        {
            var node = _nodeLibrary.CreateInstance(type, _serviceProvider);
            var nodeId = node is IGateway ? NewGatewayId()
            : node is IEvent ? NewEventId()
            : NewActivityId();
            var record = new NodeRecord { Id = nodeId, Name = name ?? node.DisplayText, OwnerId = ownerId };
            return (node, record);
        }

        private void BuildProcess(Process process, string? currentId = default, string? ownerId = default)
        {
            foreach (var step in process.Steps)
            {
                var (node, record) = CreateNode(step.Type, step.Name, ownerId);

                AddNode(record, node);

                var merged = false;
                if (node is IGateway)
                {
                    if ((step.Branches == null || step.Branches.Length == 0)
                        && (step.MergeBranches == null || step.MergeBranches.Length == 0))
                    {
                        throw new ArgumentNullException("Gateway require branches or merge branches info");
                    }
                    if (step.Branches != null && step.Branches.Length > 0)
                    {
                        foreach (var branch in step.Branches)
                        {
                            BuildProcess(branch, record.Id);
                        }
                    }
                    if (step.MergeBranches != null && step.MergeBranches.Length > 0)
                    {
                        var nodesToMerge = _nodeRecords.Values.Where(n => step.MergeBranches.Contains(n.Name)).Select(n => n.Id);
                        foreach (var leaf in nodesToMerge)
                        {
                            var flowId = NewFlowId();
                            var flowRecord = new FlowRecord
                            {
                                Id = flowId,
                                DestinationRef = record.Id,
                                SourceRef = leaf,
                                OwnerId = ownerId
                            };
                            var mFlow = new SequenceFlow();
                            AddFlow(flowRecord, mFlow);
                        }
                        merged = true;
                    }
                }
                else if (node is IActivity)
                {
                    if (step.BoundaryEvents != null)
                    {
                        foreach (var evt in step.BoundaryEvents)
                        {
                            var (evtNode, evtRecord) = CreateNode(evt.Type, evt.Name, ownerId);
                            evtRecord.AttachedToRef = record.Id;
                            AddNode(evtRecord, evtNode);
                            if (evt.Process != null)
                            {
                                BuildProcess(evt.Process, evtRecord.Id);
                            }
                        }
                    }
                    if (node is SubProcess)
                    {
                        if (step.Process == null)
                        {
                            throw new ArgumentNullException("Sub-process");
                        }
                        BuildProcess(step.Process, default, record.Id);
                    }
                }

                if (!string.IsNullOrEmpty(currentId) && !merged)
                {
                    var flow = new SequenceFlow { };
                    var flowId = NewFlowId();
                    var flowRecord = new FlowRecord
                    {
                        Id = flowId,
                        SourceRef = currentId,
                        DestinationRef = record.Id,
                        ConditionExpression = step.Condition,
                        OwnerId = ownerId
                    };

                    AddFlow(flowRecord, flow, default);
                }

                currentId = record.Id;

            }
        }
        #endregion
    }
}
