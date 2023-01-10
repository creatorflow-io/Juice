using System.Collections.Generic;
using System.Threading;

namespace Juice.Workflows.Tests
{
    internal class WorkflowTestHelper
    {
        private ITestOutputHelper _output;
        private IServiceProvider _serviceProvider;

        private IWorkflow? _executing;
        public IWorkflow? Executing => _executing;

        public WorkflowTestHelper(ITestOutputHelper output, IServiceProvider serviceProvider)
        {
            _output = output;
            _serviceProvider = serviceProvider;
        }

        public static Task<WorkflowExecutionResult?> ExecuteAsync(IServiceProvider serviceProvider,
            ITestOutputHelper output, string workflowId, Dictionary<string, object?>? input = default)
            => new WorkflowTestHelper(output, serviceProvider).StartAsync(workflowId, input);

        public static Task<WorkflowExecutionResult?> ExecuteAsync(WorkflowExecutor workflowExecutor,
            WorkflowContext workflowContext,
            ITestOutputHelper output, IServiceProvider serviceProvider,
            string? nodeId = default, Dictionary<string, object?>? input = default)
           => new WorkflowTestHelper(output, serviceProvider).ExecuteAsync(workflowExecutor, workflowContext, nodeId);

        private SemaphoreSlim _signal = new SemaphoreSlim(0);
        private Queue<string> _event = new Queue<string>();
        private Dictionary<string, int> _executed = new Dictionary<string, int>();

        public async Task<WorkflowExecutionResult?> StartAsync(
            string workflowId, Dictionary<string, object?>? input)
        {
            string? nullCorrelationId = default;
            string? nullName = default;
            using var scope = _serviceProvider.CreateScope();

            var workflow = scope.ServiceProvider.GetRequiredService<IWorkflow>();
            _executing = workflow;

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
                        return await ResumeAsync(newWorkflowId, blockings.First().Id, input);
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
                                return await ResumeAsync(newWorkflowId, eventId, input);
                            }
                        }
                        catch (OperationCanceledException)
                        {
                            if (!context.Completed)
                            {
                                return new WorkflowExecutionResult
                                {
                                    Status = WorkflowStatus.Faulted,
                                    Message = "Operation timedout"
                                };
                            }
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

        public async Task<WorkflowExecutionResult?> ResumeAsync(
            string workflowId, string nodeId, Dictionary<string, object?>? input)
        {
            using var scope = _serviceProvider.CreateScope();
            var workflow = scope.ServiceProvider.GetRequiredService<IWorkflow>();
            _executing = workflow;
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
                        return await ResumeAsync(workflowId, blocking.Id, input);
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
                                return await ResumeAsync(workflowId, eventId, input);
                            }
                        }
                        catch (OperationCanceledException)
                        {
                            if (!context.Completed)
                            {
                                return new WorkflowExecutionResult
                                {
                                    Status = WorkflowStatus.Faulted,
                                    Message = "Operation timedout"
                                };
                            }
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
