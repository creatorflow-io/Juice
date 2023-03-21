using System.Diagnostics;
using Grpc.Net.Client;
using Juice.Workflows.Grpc;
using Juice.XUnit;

namespace Juice.Workflows.Tests
{
    [TestCaseOrderer("Juice.XUnit.PriorityOrderer", "Juice.XUnit")]
    public class GrpcTests
    {
        private ITestOutputHelper _output;
        static string _correlationId = new DefaultStringIdGenerator().GenerateRandomId(8);
        private string _definitionId = "incodeWf";
        static string _workflowId = "";
        private string _grpcEndpoint = "https://localhost:7228";

        public GrpcTests(ITestOutputHelper output)
        {
            _output = output;
            Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Development");
        }

        [IgnoreOnCIFact(DisplayName = "Wf shoud start"), TestPriority(999)]
        public async Task Should_start_wf_Async()
        {
            var timer = new Stopwatch();
            timer.Start();

            var channel =
                GrpcChannel.ForAddress(new Uri(_grpcEndpoint));

            var client = new Workflow.WorkflowClient(channel);

            _output.WriteLine("Init client take {0} milliseconds",
                timer.ElapsedMilliseconds);

            _output.WriteLine("Start workflow {0}, correlationId: {1}",
              _definitionId, _correlationId);

            var reply = await client.StartAsync(
                new StartWorkflowMessage
                {
                    WorkflowId = _definitionId,
                    CorrelationId = _correlationId,
                    Name = "xunit test",
                    SerializedParameters = "{}"
                });

            if (reply != null)
            {
                _output.WriteLine("Succeeded: {0}. Message: {1}",
              reply.Succeeded, reply.Message);
                if (reply.Succeeded && !string.IsNullOrEmpty(reply.WorkflowId))
                {
                    _workflowId = reply.WorkflowId;
                    _output.WriteLine("WorkflowId: {0}.", _workflowId);
                }
            }
            _output.WriteLine("Request take {0} milliseconds",
                timer.ElapsedMilliseconds);
            timer.Stop();
            _workflowId.Should().HaveLength("b3m372n8xz4mv0sq39v3766qd4".Length);
            await Task.Delay(10000);
        }

        [IgnoreOnCIFact(DisplayName = "Wf shoud resume KB"), TestPriority(998)]
        public async Task Should_resume_wf_Async()
        {
            var resumeNodeId = "Activity_T6MTDX";
            await Task.Delay(3000);
            var timer = new Stopwatch();
            timer.Start();

            var channel =
                GrpcChannel.ForAddress(new Uri(_grpcEndpoint));

            var client = new Workflow.WorkflowClient(channel);

            _output.WriteLine("Init client take {0} milliseconds",
                timer.ElapsedMilliseconds);

            _output.WriteLine("Resume workflow {0}",
               _workflowId);

            var reply = await client.ResumeAsync(
                new ResumeWorkflowMessage
                {
                    WorkflowId = _workflowId,
                    NodeId = resumeNodeId, // KB
                    SerializedParameters = "{}"
                });

            if (reply != null)
            {
                _output.WriteLine("Succeeded: {0}. Message: {1}",
              reply.Succeeded, reply.Message);

            }
            _output.WriteLine("Request take {0} milliseconds",
                timer.ElapsedMilliseconds);
            timer.Stop();

        }

        /// <summary>
        /// Require CorrelationId that match with catching event's CorrelationId
        /// </summary>
        /// <returns></returns>
        [IgnoreOnCIFact(DisplayName = "Catch event name"), TestPriority(997)]
        public async Task Should_catch_name_Async()
        {
            await Task.Delay(3000);
            var timer = new Stopwatch();
            timer.Start();

            var channel =
                GrpcChannel.ForAddress(new Uri(_grpcEndpoint));

            var client = new Workflow.WorkflowClient(channel);

            _output.WriteLine("Init client take {0} milliseconds",
                timer.ElapsedMilliseconds);

            _output.WriteLine("Wf catch event {0}, correlationId: {1}",
              "wfcatch.uploaded.media.final", _correlationId);

            var reply = await client.CatchAsync(
                new CatchMessage
                {
                    CallbackId = "",
                    SerializedParameters = "{}",
                    EventName = "wfcatch.uploaded.media.final",
                    IsCompleted = true,
                    CorrelationId = _correlationId
                });

            if (reply != null)
            {
                _output.WriteLine("Succeeded: {0}. Message: {1}",
              reply.Succeeded, reply.Message);
            }
            _output.WriteLine("Request take {0} milliseconds",
                timer.ElapsedMilliseconds);
            timer.Stop();

        }

        /// <summary>
        /// Require CallbackId that match with catching event's Id
        /// </summary>
        /// <returns></returns>
        [IgnoreOnCIFact(DisplayName = "Catch event id"), TestPriority(90)]
        public async Task Should_catch_id_Async()
        {
            var callbackId = "acef422f-83cf-4195-9bb5-5bf874ade216";
            var timer = new Stopwatch();
            timer.Start();

            var channel =
                GrpcChannel.ForAddress(new Uri(_grpcEndpoint));

            var client = new Workflow.WorkflowClient(channel);

            _output.WriteLine("Init client take {0} milliseconds",
                timer.ElapsedMilliseconds);

            var reply = await client.CatchAsync(
                new CatchMessage
                {
                    CallbackId = callbackId,
                    SerializedParameters = "{}",
                    EventName = "",
                    IsCompleted = true
                });

            if (reply != null)
            {
                _output.WriteLine("Succeeded: {0}. Message: {1}",
              reply.Succeeded, reply.Message);
            }
            _output.WriteLine("Request take {0} milliseconds",
                timer.ElapsedMilliseconds);
            timer.Stop();

        }

    }
}
