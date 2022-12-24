namespace Juice.Workflows.Services
{
    public class WorkflowExecutor
    {
        private readonly ILogger _logger;
        // The maximum recursion depth is used to limit the number of Workflow (of any type) that a given
        // Workflow execution can trigger (directly or transitively) without reaching a blocking activity.
        private const int MaxRecursionDepth = 100;
        private int _currentRecursionDepth;
        private Queue<(NodeContext Node, FlowContext Flow)> _queue = new Queue<(NodeContext, FlowContext)>();
        public WorkflowExecutor(ILogger<WorkflowExecutor> logger)
        {
            _logger = logger;
        }

        public async Task<WorkflowExecutionResult> ExecuteAsync(WorkflowContext workflowContext, string? nodeId,
            CancellationToken token)
        {
            try
            {
                if (workflowContext == null)
                {
                    throw new ArgumentNullException(nameof(workflowContext));
                }
                workflowContext.LastMessages.Clear();
                _logger.LogInformation("============ BEGIN EXECUTE WORKFLOW {0} =============", workflowContext.WorkflowId);

                var node = !string.IsNullOrEmpty(nodeId)
                    ? workflowContext.GetNode(nodeId)
                    : workflowContext.GetStartNode(default);

                NodeExecutionResult nodeResult =
                    await ExecuteInternalAsync(workflowContext, node, default, token);
                while (_queue.TryDequeue(out var item))
                {
                    if (workflowContext.IsFinished(item.Node.Record.Id))
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
                var result = new WorkflowExecutionResult
                {
                    Context = workflowContext,
                    Message = nodeResult.Message,
                    Status = workflowContext.FaultedNodes.Any()
                    ? WorkflowStatus.Faulted
                    : workflowContext.BlockingNodes.Any()
                    ? WorkflowStatus.Halted
                    : nodeResult.Status
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
                return new NodeExecutionResult(WorkflowStatus.Faulted, "Node is null");
            }

            _currentRecursionDepth++;
            if (_currentRecursionDepth > MaxRecursionDepth || token.IsCancellationRequested)
            {
                return new NodeExecutionResult(
                     WorkflowStatus.Aborted,
                        token.IsCancellationRequested ? "The workflow execution was aborted."
                    : "The max recursion depth of Workflow executions has been reached."
                );
            }

            var isResuming = workflowContext.BlockingNodes.Any(b => b.Id == nodeContext.Record.Id);

            NodeExecutionResult nodeExecutionResult;
            try
            {
                nodeExecutionResult =
                    isResuming ? await nodeContext.Node.ResumeAsync(workflowContext, nodeContext, token)
                    : await nodeContext.Node.ExecuteAsync(workflowContext, nodeContext, flowContext, token);
            }
            catch (Exception ex)
            {
                nodeExecutionResult = NodeExecutionResult.Fault(ex.Message);
            }
            finally
            {
                _currentRecursionDepth--;
            }

            workflowContext.ProcessNodeExecutionResult(nodeContext, nodeExecutionResult);

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
                    await gateway.PostCheckAsync(workflowContext, nodeContext, token);
                }

                #endregion
            }

            return nodeExecutionResult;
        }

    }
}
