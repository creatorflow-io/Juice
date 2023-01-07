using MediatR;

namespace Juice.Workflows.Execution
{
    /// <summary>
    /// Contains workflow data, incomes, outcomes, properties, states... to execute a workflow process
    /// </summary>
    public class WorkflowContext : IDisposable
    {
        public WorkflowContext(
            string workflowId
            , string? correlationId
            , IList<NodeSnapshot>? nodeSnapshots
            , IList<FlowSnapshot>? flowSnapshots
            , IList<ProcessSnapshot>? processSnapshots
            , IDictionary<string, object?>? input
            , IDictionary<string, object?>? output
            , IEnumerable<NodeContext> nodes
            , IEnumerable<FlowContext> flows
            , IEnumerable<ProcessRecord> processes
            , string? name
            , string? user
            , string? resolvedBy
           )
        {
            WorkflowId = workflowId;
            CorrelationId = correlationId;
            Input = input ?? new Dictionary<string, object?>();
            Output = output ?? new Dictionary<string, object?>();
            Nodes = nodes.ToDictionary(x => x.Record.Id);
            Flows = flows;
            Processes = processes;
            ProcessSnapshots = processes.Select(p => new ProcessSnapshot
            {
                Id = p.Id,
                Name = p.Name,
                Status = processSnapshots?.FirstOrDefault(s => s.Id == p.Id)?.Status ?? WorkflowStatus.Idle
            }).ToList();
            Name = name;
            User = user;
            NodeSnapshots = nodeSnapshots ?? new List<NodeSnapshot>();
            FlowSnapshots = flowSnapshots ?? new List<FlowSnapshot>();
            ResolvedBy = resolvedBy;
        }

        /// <summary>
        /// Context builder
        /// </summary>
        public string? ResolvedBy { get; }

        public string? User { get; }

        public string? Name { get; }

        public string WorkflowId { get; init; }

        public string? CorrelationId { get; }

        public WorkflowState State
        {
            get
            {
                return new WorkflowState
                {
                    Input = Input,
                    Output = Output,
                    NodeSnapshots = NodeSnapshots,
                    NodeStates = Nodes.ToDictionary(x => x.Key, x => x.Value.Properties),
                    FlowSnapshots = FlowSnapshots,
                    ProcessSnapshots = ProcessSnapshots,
                    LastMessages = LastMessages.Reverse(),
                    IdlingNodes = IdlingNodes,
                    DomainEvents = _domainEvents
                };
            }
        }

        public IList<NodeSnapshot> NodeSnapshots { get; private set; }

        public IList<FlowSnapshot> FlowSnapshots { get; private set; }

        public IList<ProcessSnapshot> ProcessSnapshots { get; private set; }

        /// <summary>
        /// Blocking activities
        /// </summary>
        public IList<BlockingNode> BlockingNodes => NodeSnapshots.GetBlockingNodes().ToList();

        /// <summary>
        /// Keeps track of which activities executed in which order.
        /// </summary>
        public IList<ExecutedNode> ExecutedNodes => NodeSnapshots.GetExecutedNodes().ToList();

        /// <summary>
        /// The list of faulted activities.
        /// </summary>
        public IList<FaultedNode> FaultedNodes => NodeSnapshots.GetFaultedNodes().ToList();

        public IList<IdlingNode> IdlingNodes
        {
            get
            {
                return NodeSnapshots.Where(n => n.Status == WorkflowStatus.Idle)
                    .Select(n => new IdlingNode(n.Id, n.Name))
                    .ToList()
                    .Union(
                        Nodes.Values.Where(n =>
                            !NodeSnapshots.Any(s => s.Id == n.Record.Id))
                        .Select(n => new IdlingNode(n.Record.Id, n.Record.Name)))
                        .ToList();
            }
        }


        /// <summary>
        /// A dictionary of re-hydrated values provided by the initiator of the workflow.
        /// </summary>
        public IDictionary<string, object?> Input { get; }

        /// <summary>
        /// A dictionary of node's output
        /// </summary>
        public IDictionary<string, object?> Output { get; }

        /// <summary>
        /// Last message stack of workflow execution
        /// </summary>
        public Stack<string> LastMessages { get; } = new Stack<string>();

        /// <summary>
        /// All of activities in workflow data
        /// </summary>
        public IDictionary<string, NodeContext> Nodes { get; }

        /// <summary>
        /// A complete list of the transitions between the activities on this workflow.
        /// </summary>

        public IEnumerable<FlowContext> Flows { get; }

        public IEnumerable<ProcessRecord> Processes { get; init; }

        public bool IsNodeFinished(string id)
        {
            return ExecutedNodes.Any(n => n.Id == id);
        }

        public NodeContext? GetNode(string id)
        {
            return string.IsNullOrEmpty(id) || !Nodes.ContainsKey(id) ? null : Nodes[id];
        }

        public NodeContext GetStartNode(string? processId)
        {
            if (processId == null && Processes.Count() > 1)
            {
                throw new ArgumentNullException("Workflow contains more than one process so the processId is required");
            }
            return Nodes.Values.Single(n => n.Node is StartEvent
                && n.Record.ProcessIdRef == processId);
        }

        public IEnumerable<FlowContext> GetIncomings(NodeContext node)
        {
            return Flows.Where(x => node.Record.Incomings.Contains(x.Record.Id));
        }

        public IEnumerable<FlowContext> GetOutgoings(NodeContext node)
        {
            return node.Record.Outgoings.Select(id => Flows.Single(f => f.Record.Id == id));
        }

        public bool IsDefaultOutgoing(FlowContext flow, NodeContext? node)
        {
            node = node ?? GetNode(flow.Record.SourceRef);
            if (node == null) { throw new ArgumentNullException(nameof(node)); }
            return !string.IsNullOrEmpty(node.Record.Default)
                && node.Record.Default == flow.Record.Id;
        }

        public void ProcessNodeExecutionResult(NodeContext node, NodeExecutionResult executionResult)
        {
            if (executionResult.Status == WorkflowStatus.Finished)
            {
                #region Remove any inbound blocking activities.

                var ancestorIds = GetIncomings(node).Select(t => t.Record.SourceRef).ToArray();

                foreach (var id in ancestorIds)
                {
                    var blocking = NodeSnapshots.FirstOrDefault(f => f.Id == id && f.Status == WorkflowStatus.Halted);
                    if (blocking != null)
                    {
                        blocking.Idle(User);
                    }
                }

                #endregion
            }

            var snapshot = NodeSnapshots.FirstOrDefault(f => f.Id == node.Record.Id);
            if (snapshot != null)
            {
                snapshot.SetStatus(executionResult.Status, executionResult.Message, User, executionResult.Outcomes);
            }
            else
            {
                snapshot = new NodeSnapshot
                {
                    Id = node.Record.Id,
                    Name = node.Record.Name
                };
                snapshot.SetStatus(executionResult.Status, executionResult.Message, User, executionResult.Outcomes);

                NodeSnapshots.Add(snapshot);
            }

        }

        public void CancelBlockingEvent(string id)
        {
            var snapshot = NodeSnapshots.FirstOrDefault(n => n.Id == id);
            var node = GetNode(id).Node;
            if (snapshot != null
                && snapshot.Status == WorkflowStatus.Halted
                && node is IIntermediate
                && node is ICatching)
            {
                snapshot.Idle(User, "Cancelled because other event was cactched");
            }
        }

        public void ActiveFlow(FlowContext flow)
        {
            if (!FlowSnapshots.Any(f => f.Id == flow.Record.Id))
            {
                FlowSnapshots.Add(new FlowSnapshot { Id = flow.Record.Id, Name = flow.Record.Name });
            }
        }

        public void DeativeFlow(FlowContext flow)
        {
            if (FlowSnapshots.Any(f => f.Id == flow.Record.Id))
            {
                var removes = FlowSnapshots.Where(f => f.Id == flow.Record.Id).ToArray();
                foreach (var remove in removes)
                {
                    FlowSnapshots.Remove(remove);
                }
            }
        }

        public bool AnyActiveFlowFrom(NodeContext source)
        {
            return FlowSnapshots.Any(f =>
                 source.Record.Outgoings.Contains(f.Id));
        }

        public bool AnyActiveFlowTo(NodeContext dest, string? exceptedId)
        {
            return FlowSnapshots.Any(f => (string.IsNullOrEmpty(exceptedId) || f.Id != exceptedId)
                && dest.Record.Incomings.Contains(f.Id));
        }

        public bool AllFlowActiveTo(NodeContext dest)
        {
            return dest.Record.Incomings.All(id => FlowSnapshots.Any(s => s.Id == id));
        }

        public bool AnyIncompleteActivePathTo(NodeContext dest)
        {
            var incomings = GetIncomings(dest);
            foreach (var income in incomings)
            {
                if (!IsActiveFlow(income) && IsActivePath(income))
                {
                    return true;
                }
            }
            return false;
        }

        private bool IsActiveFlow(FlowContext flow)
        {
            return FlowSnapshots.Any(f => f.Id == flow.Record.Id);
        }
        private bool IsActivePath(FlowContext flow)
        {
            if (IsActiveFlow(flow))
            {
                return true;
            }


            var ancestor = GetNode(flow.Record.SourceRef);
            if (ancestor == null)
            {
                throw new InvalidOperationException("Ancestor node not found");
            }
            if (ancestor.Node is InclusiveGateway)
            {
                return false;
            }

            foreach (var income in GetIncomings(ancestor))
            {
                if (IsActivePath(income))
                {
                    return true;
                }
            }
            return false;
        }

        public IEnumerable<string> GetOutcomes(string nodeId)
        {
            var executed = ExecutedNodes?.SingleOrDefault(a => a.Id == nodeId);
            return executed?.Outcomes ?? new string[] { };

        }


        private IList<INotification> _domainEvents = new List<INotification>();
        public void AddDomainEvent(INotification evt)
        {
            _domainEvents.Add(evt);
        }

        public bool IsFinished
            => ProcessSnapshots.All(p => p.Status == WorkflowStatus.Finished);
        public bool HasFinishSignal(string processId)
            => ProcessSnapshots.Any(p => p.Id == processId && p.Status == WorkflowStatus.Finished);
        public void Finish(string processId)
            => ProcessSnapshots.Single(p => p.Id == processId).SetStatus(WorkflowStatus.Finished);
        public bool HasTerminated
            => ProcessSnapshots.Any(p => p.Status == WorkflowStatus.Aborted);
        public bool HasTerminateSignal(string processId)
            => ProcessSnapshots.Any(p => p.Id == processId && p.Status == WorkflowStatus.Aborted);
        public void Terminate(string processId)
            => ProcessSnapshots.Single(p => p.Id == processId).SetStatus(WorkflowStatus.Aborted);
        public void Start(string processId)
            => ProcessSnapshots.Single(p => p.Id == processId).SetStatus(WorkflowStatus.Executing);
        public bool HasFaulted
            => ProcessSnapshots.Any(p => p.Status == WorkflowStatus.Faulted);
        public void Fault(string processId)
            => ProcessSnapshots.Single(p => p.Id == processId).SetStatus(WorkflowStatus.Faulted);

        public IEnumerable<ProcessRecord> IdlingProcesses
            => Processes.Where(p => ProcessSnapshots.Where(s => s.Status == WorkflowStatus.Idle).Select(s => s.Id).Contains(p.Id));
        #region IDisposable Support

        private bool disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    //  dispose managed state (managed objects).
                    foreach (var flow in Flows)
                    {
                        flow.Flow.Dispose();
                    }
                    foreach (var node in Nodes.Values)
                    {
                        node.Node.Dispose();
                    }
                }
                disposedValue = true;
            }
        }

        //  override a finalizer only if Dispose(bool disposing) above has code to free unmanaged resources.
        ~WorkflowContext()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(false);
        }

        // This code added to correctly implement the disposable pattern.
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        #endregion
    }
}
