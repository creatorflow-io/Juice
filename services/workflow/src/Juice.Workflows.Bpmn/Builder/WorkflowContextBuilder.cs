using System.Xml.Serialization;
using Juice.Services;
using Juice.Workflows.Bpmn.Models;
using Juice.Workflows.Builder;
using Juice.Workflows.Domain.AggregatesModel.WorkflowAggregate;
using Juice.Workflows.Execution;
using Juice.Workflows.Models;
using Juice.Workflows.Nodes.Activities;
using Juice.Workflows.Nodes.Events;
using Juice.Workflows.Services;

namespace Juice.Workflows.Bpmn.Builder
{
    public class WorkflowContextBuilder : WorkflowContextBuilderBase
    {

        public WorkflowContextBuilder(
            IStringIdGenerator stringIdGenerator,
            INodeLibrary nodeLibrary,
            IServiceProvider serviceProvider
        ) : base(stringIdGenerator, nodeLibrary, serviceProvider)
        {

        }

        public WorkflowContext Build(TextReader? reader, WorkflowRecord workflow,
            bool rebuild = false)
        {
            var name = workflow.Name;
            if (_needBuild || rebuild)
            {
                _needBuild = false;
                if (reader == null)
                {
                    throw new ArgumentNullException("reader");
                }
                var serializer =
                    new XmlSerializer(typeof(tDefinitions));
                var bpmn = (tDefinitions?)serializer.Deserialize(reader);
                if (bpmn == null)
                {
                    throw new Exception("Bpmn xml can not be deserialize.");
                }
                if (!bpmn.Process.Any())
                {
                    throw new Exception("Bpmn xml does not contain any workflow process definition.");
                }

                if (string.IsNullOrEmpty(name) && bpmn.name != null)
                {
                    name = bpmn.name;
                }

                foreach (var process in bpmn.Process)
                {
                    if (process.flowElement == null)
                    {
                        throw new Exception("Bpmn process does not contain any flow element.");
                    }
                    var processId = process.id;
                    if (string.IsNullOrEmpty(name))
                    {
                        name = process.name;
                    }

                    AddProcess(new ProcessRecord(processId, process.name));

                    BuildProcess(process.flowElement, processId);
                }
            }

            return new WorkflowContext(workflow.Id
                , name
                , _nodeRecords.Values.Select(n => new NodeContext(n, _nodes[n.Id])).ToList()
                , _flowRecords.Values.Select(f => new FlowContext(f, _flows[f.Id])).ToList()
                , _processRecords.Values
                , this.GetType().FullName
                );
        }

        #region Builder

        private void BuildProcess(tFlowElement[] flowElements, string processId)
        {

            foreach (var element in flowElements.OfType<tFlowNode>())
            {

                if (Constants.NodeTypesMapping.ContainsKey(element.GetType().Name))
                {
                    AddMappedNode(element, processId);
                }
                else if (element is tSubProcess subProcess)
                {
                    AddNode<SubProcess>(element, processId);
                    BuildProcess(subProcess.flowElement, subProcess.id);
                }
                else if (element is tBoundaryEvent boundaryEvent)
                {
                    if (boundaryEvent.IsError())
                    {
                        AddBoundaryEvent<BoundaryErrorEvent>(boundaryEvent, boundaryEvent.attachedToRef.Name, processId);
                    }
                    else if (boundaryEvent.IsTimer())
                    {
                        AddBoundaryEvent<BoundaryTimerEvent>(boundaryEvent, boundaryEvent.attachedToRef.Name, processId);
                    }
                    else
                    {
                        AddUnknownNode(boundaryEvent, processId);
                    }
                }
                else if (element is tStartEvent startEvent)
                {
                    if (startEvent.IsSignal())
                    {
                        AddNode<SignalStartEvent>(element, processId);
                    }
                    else if (startEvent.IsTimer())
                    {
                        AddNode<TimerStartEvent>(element, processId);
                    }
                    else
                    {
                        AddNode<StartEvent>(element, processId);
                    }
                }
                else if (element is tEndEvent endEvent)
                {
                    if (endEvent.IsCancel())
                    {
                        AddNode<CancelEvent>(element, processId);
                    }
                    else if (endEvent.IsTerminate())
                    {
                        AddNode<TerminateEvent>(element, processId);
                    }
                    else
                    {
                        AddNode<EndEvent>(element, processId);
                    }
                }
                else
                {
                    AddUnknownNode(element, processId);
                }
            }
            foreach (var flow in flowElements.OfType<tSequenceFlow>())
            {
                AddFlow(flow, processId);
            }
        }

        private void AddMappedNode(tFlowNode flowNode, string processId)
        {
            var (node, record) = CreateNode(Constants.NodeTypesMapping[flowNode.GetType().Name],
                flowNode.id, flowNode.name, processId);
            AddNodeInternal(flowNode, node, record);
        }

        private void AddNode<T>(tFlowNode flowNode, string processId)
            where T : class, INode
        {
            var (node, record) = CreateNode(typeof(T).Name, flowNode.id, flowNode.name, processId);
            AddNodeInternal(flowNode, node, record);
        }

        private void AddUnknownNode(tFlowNode flowNode, string processId)
        {
            var (node, record) = CreateNode(flowNode.GetType().Name, flowNode.id, flowNode.name, processId);
            AddNodeInternal(flowNode, node, record);
        }

        private void AddBoundaryEvent<T>(tBoundaryEvent flowNode, string attachedToRef, string processId)
            where T : class, IBoundary
        {
            var (node, record) = CreateNode(typeof(T).Name, flowNode.id, flowNode.name, processId);
            record.AttachTo(attachedToRef);
            if (!flowNode.cancelActivity)
            {
                ((T)node).NonInterupt();
            }
            AddNodeInternal(flowNode, node, record);
        }

        private void AddNodeInternal(tFlowNode flowNode, INode node, NodeRecord record)
        {
            if (flowNode.incoming != null)
            {
                foreach (var incoming in flowNode.incoming)
                {
                    record.AddIncoming(incoming.Name);
                }
            }
            if (flowNode.outgoing != null)
            {
                foreach (var outgoing in flowNode.outgoing)
                {
                    record.AddOutgoing(outgoing.Name);
                }
            }

            _nodeRecords[record.Id] = record;
            _nodes[record.Id] = node;
        }

        private (INode Node, NodeRecord Record) CreateNode(string type, string id, string? name, string processId)
        {
            var node = _nodeLibrary.CreateInstance(type, _serviceProvider);
            var record = new NodeRecord { Id = id, Name = name ?? node.DisplayText, ProcessIdRef = processId };
            return (node, record);
        }

        private void AddFlow(tSequenceFlow flow, string processId)
        {
            var id = flow.id;
            var record = new FlowRecord(id, flow.sourceRef, flow.targetRef, flow.name, processId,
                flow.conditionExpression != null ? string.Join(' ', flow.conditionExpression.Text) : default);
            var sequenceFlow = new SequenceFlow();

            _flowRecords[record.Id] = record;
            _flows[record.Id] = sequenceFlow;
        }

        #endregion
    }
}
