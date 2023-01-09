using System.Collections.Generic;
using System.Threading;

namespace Juice.Workflows.Tests
{
    internal class WorkflowTestHelper
    {
        private ITestOutputHelper _output;

        public WorkflowTestHelper(ITestOutputHelper output)
        {
            _output = output;
        }

        public static Task<WorkflowExecutionResult?> ExecuteAsync(IWorkflow workflow,
            ITestOutputHelper output, string workflowId, Dictionary<string, object?>? input = default)
            => new WorkflowTestHelper(output).StartAsync(workflow, workflowId, input);

        public static Task<WorkflowExecutionResult?> ExecuteAsync(WorkflowExecutor workflowExecutor,
            WorkflowContext workflowContext,
            ITestOutputHelper output,
            string? nodeId = default, Dictionary<string, object?>? input = default)
           => new WorkflowTestHelper(output).ExecuteAsync(workflowExecutor, workflowContext, nodeId);

        private SemaphoreSlim _signal = new SemaphoreSlim(0);
        private Queue<string> _event = new Queue<string>();
        private Dictionary<string, int> _executed = new Dictionary<string, int>();

        public async Task<WorkflowExecutionResult?> StartAsync(IWorkflow workflow,
            string workflowId, Dictionary<string, object?>? input)
        {
            string? nullCorrelationId = default;
            string? nullName = default;
            var rs = await workflow.StartAsync(workflowId, nullCorrelationId, nullName, input);
            if (rs.Succeeded)
            {
                _output.WriteLine("Start workflow status " + rs.Data.Status);
                _output.WriteLine("Start workflow message " + rs.Data.Message);
                _output.WriteLine("Started workflow id " + rs.Data.Context.WorkflowId);

                var context = rs.Data.Context;
                var newWorkflowId = context.WorkflowId;
                if (context?.State?.BlockingNodes != null)
                {
                    var blockings = context.State
                        .BlockingNodes.Where(b =>
                            context.GetNode(b.Id).Node is IActivity);
                    if (blockings.Any())
                    {
                        return await ResumeAsync(workflow, newWorkflowId, blockings.First().Id, input);
                    }

                    var events = context.State
                        .BlockingNodes.Where(b =>
                            context.GetNode(b.Id).Node is IEvent);

                    if (events.Any())
                    {
                        try
                        {
                            var tokenSource = new CancellationTokenSource(3000);
                            await _signal.WaitAsync(tokenSource.Token);
                            if (_event.TryDequeue(out var eventId))
                            {
                                return await ResumeAsync(workflow, newWorkflowId, eventId, input);
                            }
                        }
                        catch (OperationCanceledException)
                        {
                            return new WorkflowExecutionResult
                            {
                                Status = WorkflowStatus.Faulted,
                                Message = "Operation timedout"
                            };
                        }
                    }
                }
                return rs.Data;
            }
            else
            {
                _output.WriteLine(rs.Message);
            }
            return default;
        }

        public async Task<WorkflowExecutionResult?> ResumeAsync(IWorkflow workflow,
            string workflowId, string nodeId, Dictionary<string, object?>? input)
        {
            var rs = await workflow.ResumeAsync(workflowId, nodeId, input);
            if (rs.Succeeded)
            {
                IncraseExecutedCount(nodeId);
                _output.WriteLine("Execute workflow status " + rs.Data.Status);
                _output.WriteLine("Execute workflow message " + rs.Data.Message);

                var context = rs.Data.Context;
                if (context?.State?.BlockingNodes != null)
                {
                    var blockings = context.State
                        .BlockingNodes.Where(b =>
                            context.GetNode(b.Id).Node is IActivity);
                    if (blockings.Any())
                    {
                        var blocking = blockings.FirstOrDefault(b =>
                            !(context.GetNode(b.Id).Node is SubProcess))
                            ?? blockings.First();
                        return await ResumeAsync(workflow, workflowId, blocking.Id, input);
                    }

                    var events = context.State
                        .BlockingNodes.Where(b =>
                            context.GetNode(b.Id).Node is IEvent);

                    if (events.Any())
                    {
                        try
                        {
                            var tokenSource = new CancellationTokenSource(3000);
                            await _signal.WaitAsync(tokenSource.Token);
                            if (_event.TryDequeue(out var eventId))
                            {
                                return await ResumeAsync(workflow, workflowId, eventId, input);
                            }
                        }
                        catch (OperationCanceledException)
                        {
                            return new WorkflowExecutionResult
                            {
                                Status = WorkflowStatus.Faulted,
                                Message = "Operation timedout"
                            };
                        }
                    }
                }
                return rs.Data;
            }
            else
            {
                _output.WriteLine(rs.Message);
            }
            return default;
        }

        public async Task<WorkflowExecutionResult?> ExecuteAsync(WorkflowExecutor workflowExecutor,
            WorkflowContext workflowContext, string? nodeId = default)
        {

            var rs = await workflowExecutor.ExecuteAsync(workflowContext, nodeId, default);
            if (nodeId != null)
            {
                IncraseExecutedCount(nodeId);
            }
            _output.WriteLine("Execute workflow status " + rs.Status);
            _output.WriteLine("Execute workflow message " + rs.Message);

            var context = rs.Context;
            if (context?.State?.BlockingNodes != null)
            {
                var blockings = context.State
                    .BlockingNodes.Where(b =>
                        context.GetNode(b.Id).Node is IActivity);
                if (blockings.Any())
                {
                    return await ExecuteAsync(workflowExecutor, context, blockings.First().Id);
                }

                var events = context.State
                    .BlockingNodes.Where(b =>
                        context.GetNode(b.Id).Node is IEvent);

                if (events.Any())
                {
                    try
                    {
                        var tokenSource = new CancellationTokenSource(3000);
                        await _signal.WaitAsync(tokenSource.Token);
                        if (_event.TryDequeue(out var eventId))
                        {
                            return await ExecuteAsync(workflowExecutor, context, eventId);
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        return new WorkflowExecutionResult
                        {
                            Status = WorkflowStatus.Faulted,
                            Message = "Operation timedout"
                        };
                    }
                }
            }
            return rs;

        }

        public void Catched(string eventId)
        {
            _event.Enqueue(eventId);
            _signal.Release();
        }

        private void IncraseExecutedCount(string nodeId)
        {
            if (!_executed.ContainsKey(nodeId))
            {
                _executed[nodeId] = 1;
            }
            else
            {
                _executed[nodeId]++;
            }
            if (_executed[nodeId] > 3)
            {
                throw new InvalidOperationException($"Node was executed {_executed[nodeId]} times");
            }
        }
    }
}
