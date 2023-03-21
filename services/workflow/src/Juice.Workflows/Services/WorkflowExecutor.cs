namespace Juice.Workflows.Services
{
    public class WorkflowExecutor
    {
        private readonly ILogger<WorkflowExecutor> _logger;
        // The maximum recursion depth is used to limit the number of Workflow (of any type) that a given
        // Workflow execution can trigger (directly or transitively) without reaching a blocking activity.
        private const int MaxRecursionDepth = 100;
        private int _currentRecursionDepth;
        private Queue<(NodeContext Node, FlowContext? Flow)> _queue = new Queue<(NodeContext, FlowContext?)>();

        private bool _hasFailure = false;
        private bool _hasBlocking = false;

        public WorkflowExecutor(ILogger<WorkflowExecutor> logger)
        {
            _logger = logger;
        }

        public async Task<WorkflowExecutionResult> ExecuteAsync(WorkflowContext workflowContext, string? nodeId,
            CancellationToken token)
        {
            if (workflowContext == null)
            {
                throw new ArgumentNullException(nameof(workflowContext));
            }
            if (nodeId == null)
            {
                if (workflowContext.Processes.Any())
                {
                    WorkflowExecutionResult? result = null;

                    foreach (var process in workflowContext.IdlingProcesses)
                    {
                        result = await StartProcessAsync(workflowContext, process, token);
                    }
                    return result;
                }
                else
                {
                    var start = workflowContext.GetStartNode(default);
                    return await ExecuteAsync(workflowContext, start, token);
                }
            }
            else
            {
                var node = workflowContext.GetNode(nodeId);
                if (node == null)
                {
                    return new WorkflowExecutionResult
                    {
                        Context = workflowContext,
                        Message = $"Workflow context has not any node that matched id {nodeId}",
                        Status = WorkflowStatus.Faulted,
                        IsExecuted = false
                    };
                }
                else if (!workflowContext.IsResumable(node))
                {
                    return new WorkflowExecutionResult
                    {
                        Context = workflowContext,
                        Message = $"Node {node.DisplayName} is not resumable",
                        Status = WorkflowStatus.Idle,
                        IsExecuted = false
                    };
                }
                else if (workflowContext.Completed)
                {
                    var contextStatus = workflowContext.HasTerminated ? WorkflowStatus.Aborted
                            : workflowContext.IsFinished ? WorkflowStatus.Finished
                            : WorkflowStatus.Faulted;
                    return new WorkflowExecutionResult
                    {
                        Context = workflowContext,
                        Message = $"Workflow is {contextStatus}.",
                        Status = contextStatus,
                        IsExecuted = false
                    };
                }
                return await ExecuteAsync(workflowContext, node, token);
            }
        }

        public async Task<WorkflowExecutionResult> ExecuteAsync(WorkflowContext workflowContext, NodeContext? node,
            CancellationToken token)
        {
            try
            {
                _logger.LogInformation("============ BEGIN EXECUTE WORKFLOW {0} =============", workflowContext.WorkflowId);

                if (workflowContext == null)
                {
                    throw new ArgumentNullException(nameof(workflowContext));
                }
                workflowContext.LastMessages.Clear();

                if (node == null)
                {
                    throw new ArgumentNullException(nameof(node));
                }

                NodeExecutionResult nodeResult =
                    await ExecuteInternalAsync(workflowContext, node, default, token);
                while (_queue.TryDequeue(out var item))
                {
                    if (workflowContext.IsNodeFinished(item.Node.Record.Id))
                    {
                        continue;
                    }
                    nodeResult = await ExecuteInternalAsync(workflowContext, item.Node, item.Flow, token);

                    if (!string.IsNullOrEmpty(nodeResult.Message))
                    {
                        workflowContext.LastMessages.Push(nodeResult.Message);
                    }

                }

                #region Post process
                var idling = workflowContext.IdlingProcesses;
                if (workflowContext.HasFinishSignal(node.Record.ProcessIdRef)
                    && idling.Any())
                {
                    await StartProcessAsync(workflowContext, idling.First(), token);
                }

                var result = new WorkflowExecutionResult
                {
                    Context = workflowContext,
                    Message = nodeResult.Message,
                    Status =
                            workflowContext.HasTerminated ? WorkflowStatus.Aborted
                            : workflowContext.IsFinished ? WorkflowStatus.Finished
                            : _hasFailure || workflowContext.HasFaulted ? WorkflowStatus.Faulted
                            : _hasBlocking ? WorkflowStatus.Halted
                            : nodeResult.Status,
                    IsExecuted = true
                };
                if (string.IsNullOrEmpty(result.Message) && workflowContext.LastMessages.Any())
                {
                    result.Message = workflowContext.LastMessages.Reverse().First();
                }

                #endregion

                return result;
            }
            finally
            {
                _logger.LogInformation("============ END EXECUTE WORKFLOW {0} =============", workflowContext.WorkflowId);
            }
        }

        private async Task<WorkflowExecutionResult> StartProcessAsync(WorkflowContext workflowContext, ProcessRecord process,
            CancellationToken token)
        {
            var start = workflowContext.GetStartNode(process.Id);
            var result = await ExecuteAsync(workflowContext, start, token);
            if (result.Status == WorkflowStatus.Faulted)
            {
                workflowContext.Fault(process.Id);
                return result;
            }
            else
            {
                workflowContext.Start(process.Id);
            }
            return result;
        }

        /// <summary>
        /// Execute activities hierarchy
        /// </summary>
        /// <param name="workflowContext"></param>
        /// <param name="nodeContext"></param>
        /// <param name="token"></param>
        /// <returns></returns>
        private async Task<NodeExecutionResult> ExecuteInternalAsync(WorkflowContext workflowContext,
            NodeContext? nodeContext,
            FlowContext? flowContext,
            CancellationToken token)
        {

            if (nodeContext == null)
            {
                return new("Node is null", WorkflowStatus.Faulted);
            }

            if (workflowContext.HasTerminateSignal(nodeContext.Record.ProcessIdRef))
            {
                return new("The executing workflow has terminated", WorkflowStatus.Idle);
            }
            if (workflowContext.HasFinishSignal(nodeContext.Record.ProcessIdRef))
            {
                return new("The executing workflow has finished", WorkflowStatus.Idle);
            }
            if (workflowContext.IsNodeFinished(nodeContext.Record.Id))
            {
                return new("The executing node has finished", WorkflowStatus.Finished);
            }
            if (workflowContext.IsNodeAborted(nodeContext.Record.Id))
            {
                return new("The executing node has aborted", WorkflowStatus.Aborted);
            }

            _currentRecursionDepth++;
            if (_currentRecursionDepth > MaxRecursionDepth || token.IsCancellationRequested)
            {
                return new(
                     token.IsCancellationRequested ? "The workflow execution was aborted."
                    : "The max recursion depth of Workflow executions has been reached.",
                     WorkflowStatus.Aborted
                );
            }

            var isResuming = workflowContext.BlockingNodes.Any(b => b.Id == nodeContext.Record.Id);

            NodeExecutionResult nodeExecutionResult;
            try
            {
                nodeExecutionResult =
                    isResuming ? await nodeContext.Node.ResumeAsync(workflowContext, nodeContext, token)
                    : await nodeContext.Node.StartAsync(workflowContext, nodeContext, flowContext, token);

            }
            catch (Exception ex)
            {
                nodeExecutionResult = NodeExecutionResult.Fault("Failed to execute node. " + ex.Message);
            }
            finally
            {
                _currentRecursionDepth--;
            }

            workflowContext.ProcessNodeExecutionResult(nodeContext, nodeExecutionResult);
            try
            {
                await ExecuteBoundaryEventsAsync(workflowContext, nodeContext, token);
            }
            catch (Exception ex)
            {
                nodeExecutionResult = NodeExecutionResult.Fault("Failed to execute boundary events. " + ex.Message);
            }
            if (nodeExecutionResult.Status == WorkflowStatus.Finished)
            {
                #region Continue execute next nodes
                var outgoings = workflowContext.GetOutgoings(nodeContext);

                #region Conditional flow
                foreach (var flow in outgoings.Where(f =>
                    !workflowContext.IsDefaultOutgoing(f, nodeContext)
                    && !string.IsNullOrEmpty(f.Record.ConditionExpression)
                    ))
                {
                    var node = workflowContext.GetNode(flow.Record.DestinationRef);
                    if (node == null)
                    {
                        return new NodeExecutionResult
                        (
                            WorkflowStatus.Faulted,
                            "Node not found"
                        );
                    }
                    if (await flow.Flow.PreSelectCheckAsync(workflowContext, nodeContext, node, flow))
                    {
                        workflowContext.ActiveFlow(flow);
                        _queue.Enqueue((node, flow));
                    }
                }
                #endregion

                #region Default flow
                foreach (var flow in outgoings.Where(f =>
                   workflowContext.IsDefaultOutgoing(f, nodeContext)
                   || string.IsNullOrEmpty(f.Record.ConditionExpression)
                   ))
                {
                    var node = workflowContext.GetNode(flow.Record.DestinationRef);
                    if (node == null)
                    {
                        return new NodeExecutionResult
                        (
                            WorkflowStatus.Faulted,
                            "Node not found"
                        );
                    }
                    if (await flow.Flow.PreSelectCheckAsync(workflowContext, nodeContext, node, flow))
                    {
                        workflowContext.ActiveFlow(flow);
                        _queue.Enqueue((node, flow));
                    }
                }
                #endregion

                if (nodeContext.Node is IGateway gateway)
                {
                    await gateway.PostExecuteCheckAsync(workflowContext, nodeContext, token);
                }

                #region Sub-process finished
                if (nodeContext.Node is EndEvent endEvent)
                {
                    var subProcess = workflowContext.GetNode(nodeContext.Record.ProcessIdRef);
                    if (subProcess != null)
                    {
                        _queue.Enqueue((subProcess, default));
                    }
                }
                #endregion

                #endregion
            }
            else if (nodeExecutionResult.Status == WorkflowStatus.Faulted)
            {
                _hasFailure = true;
                var subProcess = workflowContext.GetNode(nodeContext.Record.ProcessIdRef);
                if (subProcess != null)
                {
                    _queue.Enqueue((subProcess, default));
                }
            }
            else if (nodeExecutionResult.Status == WorkflowStatus.Halted)
            {
                _hasBlocking = true;
            }

            return nodeExecutionResult;
        }

        protected async Task ExecuteBoundaryEventsAsync(WorkflowContext workflowContext, NodeContext node, CancellationToken token)
        {
            var boundaryEvents = workflowContext.Nodes.Values
                .Where(n => n.Node is IBoundary && (n.Record.AttachedToRef == node.Record.Id))
                .ToList();

            var isFinished = workflowContext.IsNodeFinished(node.Record.Id);
            foreach (var boundaryEvent in boundaryEvents)
            {
                if (!isFinished
                    && workflowContext.IdlingNodes.Any(n => n.Id == boundaryEvent.Record.Id)
                    && await ((IBoundary)boundaryEvent.Node).PreStartCheckAsync(workflowContext, boundaryEvent, node, token))
                {
                    var executor = new WorkflowExecutor(_logger);
                    var rs = await executor.ExecuteAsync(workflowContext, boundaryEvent, token);
                    if (rs.Status == WorkflowStatus.Faulted)
                    {
                        throw new InvalidOperationException($"Failed to execute event {boundaryEvent.DisplayName}. " + (rs.Message ?? ""));
                    }
                }
                else if (isFinished
                   && workflowContext.BlockingNodes.Any(n => n.Id == boundaryEvent.Record.Id))
                {
                    // abort event
                    workflowContext.ProcessNodeExecutionResult(boundaryEvent, new("Ancestor node has finished", WorkflowStatus.Aborted));
                }
            }

        }

    }
}
