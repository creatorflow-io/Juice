using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Juice.AspNetCore.Idempotency.Tests
{
    // US2: a retry arriving while the first request is still processing gets a clear 409 in-progress,
    // and never starts a parallel execution.
    public class InProgressResponseTests
    {
        private static HttpRequestMessage Post(string key, object body)
        {
            var req = new HttpRequestMessage(HttpMethod.Post, "/orders")
            {
                Content = JsonContent.Create(body)
            };
            req.Headers.Add("Idempotency-Key", key);
            return req;
        }

        [Fact]
        public async Task Concurrent_retry_while_processing_gets_409_and_no_second_execution_Async()
        {
            var gate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            using var host = await IdempotencyTestHost.CreateAsync(gate);
            var client = host.GetTestClient();
            var counter = host.Services.GetRequiredService<CallCounter>();

            // First request enters the handler and blocks on the gate (record is InProgress).
            var firstTask = client.SendAsync(Post("race-key-1", new { item = "book" }));

            // Spin until the handler has actually started, so the second request truly races an in-flight record.
            var spins = 0;
            while (counter.Count == 0 && spins++ < 500)
            {
                await Task.Delay(10);
            }
            counter.Count.Should().Be(1);

            // Second request with the same key, while the first is still in flight → 409.
            var second = await client.SendAsync(Post("race-key-1", new { item = "book" }));
            second.StatusCode.Should().Be(HttpStatusCode.Conflict);
            counter.Count.Should().Be(1); // no parallel execution

            // Release the first request and confirm it completed.
            gate.SetResult(true);
            var first = await firstTask;
            first.StatusCode.Should().Be(HttpStatusCode.OK);
        }
    }
}
