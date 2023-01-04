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
    public class WorkflowContextBuilder : WorkflowContextBuilderBase
    {
        public WorkflowContextBuilder(
            IStringIdGenerator stringIdGenerator,
            INodeLibrary nodeLibrary,
            IServiceProvider serviceProvider
        ) : base(stringIdGenerator, nodeLibrary, serviceProvider)
        {

        }

        public WorkflowContext Build(string? yml, WorkflowRecord workflow,
            WorkflowState? state, string? user,
            Dictionary<string, object?>? input, bool rebuild = false)
        {
            var name = workflow.Name;
            if (_needBuild || rebuild)
            {
                var deserializer = new DeserializerBuilder()
                .WithNamingConvention(UnderscoredNamingConvention.Instance)  // see height_in_inches in sample yml 
                .Build();
                if (yml == null)
                {
                    throw new ArgumentNullException("yml");
                }
                var processes = deserializer.Deserialize<Process[]>(yml);
                foreach (var process in processes)
                {
                    name = name ?? process.Name;
                    var processId = NewProcessId();
                    AddProcess(new ProcessRecord(processId, process.Name));
                    BuildProcess(process, default, processId);
                }
                _needBuild = false;
            }

            return new WorkflowContext(workflow.Id
                , workflow.CorrelationId
                , state?.NodeSnapshots
                , state?.FlowSnapshots
                , input
                , state?.Output
                , _nodeRecords.Values.Select(n => new NodeContext(n, _nodes[n.Id])).ToList()
                , _flowRecords.Values.Select(f => new FlowContext(f, _flows[f.Id])).ToList()
                , _processRecords.Values
                , name
                , user
                );
        }


        #region Builder
        private void BuildProcess(Process process, string? currentId = default, string? processId = default)
        {
            foreach (var step in process.Steps)
            {
                var (node, record) = CreateNode(step.Type, step.Name, processId);

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
                            var flowRecord = new FlowRecord(flowId, leaf, record.Id, default, processId, default);
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
                            var (evtNode, evtRecord) = CreateNode(evt.Type, evt.Name, processId);
                            evtRecord.AttachTo(record.Id);
                            AddNode(evtRecord, evtNode);
                            if (evt.Process != null)
                            {
                                BuildProcess(evt.Process, evtRecord.Id, processId);
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
                    var flowRecord = new FlowRecord(flowId, currentId, record.Id, default, processId, step.Condition);

                    AddFlow(flowRecord, flow, default);
                }

                currentId = record.Id;

            }
        }
        #endregion
    }

}
