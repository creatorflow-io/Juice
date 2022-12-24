using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Juice.Workflows.Services;

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
            ITestOutputHelper output, string workflowId)
            => new WorkflowTestHelper(output).StartAsync(workflow, workflowId);

        public static Task<WorkflowExecutionResult?> ExecuteAsync(WorkflowExecutor workflowExecutor,
            WorkflowContext workflowContext,
            ITestOutputHelper output,
            string? nodeId = default)
           => new WorkflowTestHelper(output).ExecuteAsync(workflowExecutor, workflowContext, nodeId);

        private SemaphoreSlim _signal = new SemaphoreSlim(0);
        private Queue<string> _event = new Queue<string>();

        public async Task<WorkflowExecutionResult?> StartAsync(IWorkflow workflow,
            string workflowId)
        {
            var rs = await workflow.StartAsync(workflowId, default);
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
                        return await ResumeAsync(workflow, newWorkflowId, blockings.First().Id);
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
                                return await ResumeAsync(workflow, newWorkflowId, eventId);
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
            string workflowId, string nodeId)
        {
            var rs = await workflow.ResumeAsync(workflowId, nodeId);
            if (rs.Succeeded)
            {
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
                        return await ResumeAsync(workflow, workflowId, blocking.Id);
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
                                return await ResumeAsync(workflow, workflowId, eventId);
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
    }
}
