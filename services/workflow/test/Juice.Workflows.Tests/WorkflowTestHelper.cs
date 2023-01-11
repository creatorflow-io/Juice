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

        private Dictionary<string, int> _executed = new Dictionary<string, int>();

        public async Task<WorkflowExecutionResult?> StartAsync(
            string workflowId, Dictionary<string, object?>? input)
        {
            string? nullCorrelationId = default;
            string? nullName = default;
            using var scope = _serviceProvider.CreateScope();

            var workflow = scope.ServiceProvider.GetRequiredService<IWorkflow>();
            _executing = workflow;
            var queue = scope.ServiceProvider.GetRequiredService<EventQueue>();
            var rs = await workflow.StartAsync(workflowId, nullCorrelationId, nullName, input);
            if (rs.Succeeded)
            {

                _output.WriteLine("Start workflow status " + rs.Data.Status);
                _output.WriteLine("Start workflow message " + rs.Data.Message);
                _output.WriteLine("Started workflow id " + rs.Data.Context.WorkflowId);
                var executionResult = rs.Data;
                var newWorkflowId = executionResult.Context.WorkflowId;

                var tokenSource = new CancellationTokenSource(3000);
                while (!tokenSource.IsCancellationRequested)
                {
                    var context = executionResult.Context;
                    if (context.Completed)
                    {
                        break;
                    }
                    if (context?.State?.BlockingNodes != null)
                    {
                        var blockings = context.State
                            .BlockingNodes.Where(b =>
                            {
                                var node = context.GetNode(b.Id).Node;
                                return node is IActivity && node is not SubProcess;
                            });
                        if (blockings.Any())
                        {
                            var blocking = blockings.First();
                            _output.WriteLine("Resume " + blocking.Name + " " + blocking.Id);
                            var resumeResult = await ResumeAsync(newWorkflowId, blocking.Id, input);
                            if (resumeResult != null)
                            {
                                if (resumeResult.Message != null)
                                {
                                    _output.WriteLine("Resume message: " + resumeResult.Message);
                                }
                                executionResult = resumeResult;
                            }
                            continue;
                        }
                    }

                    var events = context.State
                        .BlockingNodes.Where(b =>
                            context.GetNode(b.Id).Node is IEvent);

                    if (events.Any())
                    {
                        try
                        {
                            var eventId = await queue.TryCatchAsync(tokenSource.Token);
                            if (eventId != null)
                            {
                                _output.WriteLine("Resume " + eventId);
                                var resumeResult = await ResumeAsync(newWorkflowId, eventId, input);
                                if (resumeResult != null)
                                {
                                    if (resumeResult.Message != null)
                                    {
                                        _output.WriteLine("Resume message: " + resumeResult.Message);
                                    }
                                    executionResult = resumeResult;
                                    continue;
                                }
                            }
                        }
                        catch (OperationCanceledException)
                        {
                        }
                    }

                    await Task.Delay(100);
                }

                return executionResult;
            }
            else
            {
                _output.WriteLine(rs.Message);
            }
            return default;
        }

        private async Task<WorkflowExecutionResult?> ResumeAsync(
            string workflowId, string nodeId, Dictionary<string, object?>? input)
        {
            IncraseExecutedCount(nodeId);
            using var scope = _serviceProvider.CreateScope();
            var workflow = scope.ServiceProvider.GetRequiredService<IWorkflow>();
            _executing = workflow;
            var rs = await workflow.ResumeAsync(workflowId, nodeId, input);
            return rs?.Data;
        }

        public async Task<WorkflowExecutionResult?> ExecuteAsync(WorkflowExecutor workflowExecutor,
            WorkflowContext workflowContext, string? nodeId = default)
        {
            var queue = _serviceProvider.GetRequiredService<EventQueue>();
            var tokenSource = new CancellationTokenSource(3000);
            WorkflowExecutionResult? result = await workflowExecutor.ExecuteAsync(workflowContext, nodeId, default);
            while (!tokenSource.IsCancellationRequested)
            {
                var context = result.Context;
                if (context.Completed)
                {
                    break;
                }
                if (context?.State?.BlockingNodes != null)
                {
                    var blockings = context.State
                        .BlockingNodes.Where(b =>
                        {
                            var node = context.GetNode(b.Id).Node;
                            return node is IActivity && node is not SubProcess;
                        });

                    if (blockings.Any())
                    {
                        var blockingId = blockings.First().Id;
                        var rs = await workflowExecutor.ExecuteAsync(workflowContext, blockingId, default);
                        if (rs != null)
                        {
                            result = rs;
                            IncraseExecutedCount(blockingId);
                            _output.WriteLine("Execute workflow status " + rs.Status);
                            _output.WriteLine("Execute workflow message " + rs.Message);
                            continue;

                        }
                    }

                    var events = context.State
                        .BlockingNodes.Where(b =>
                            context.GetNode(b.Id).Node is IEvent);

                    if (events.Any())
                    {
                        try
                        {
                            var eventId = await queue.TryCatchAsync(tokenSource.Token);
                            if (eventId != null)
                            {
                                var rs = await workflowExecutor.ExecuteAsync(workflowContext, eventId, default);
                                if (rs != null)
                                {
                                    result = rs;
                                    IncraseExecutedCount(eventId);
                                    _output.WriteLine("Execute workflow status " + rs.Status);
                                    _output.WriteLine("Execute workflow message " + rs.Message);
                                    continue;

                                }
                            }
                        }
                        catch (OperationCanceledException)
                        {
                        }
                    }
                }
                await Task.Delay(100);
            }
            return result;
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
                throw new InvalidOperationException($"Node {nodeId} was executed {_executed[nodeId]} times");
            }
        }
    }
}
