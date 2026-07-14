using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Juice.XUnit;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Juice.AspNetCore.Idempotency.Tests
{
    // US1: a duplicate (same key + payload) replays the stored outcome and does not re-execute.
    public class DuplicateRequestTests
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

        [InitializeMessageContext]
        [Fact]
        public async Task Same_key_and_payload_runs_once_and_replays_Async()
        {
            using var host = await IdempotencyTestHost.CreateAsync();
            var client = host.GetTestClient();
            var counter = host.Services.GetRequiredService<CallCounter>();

            var first = await client.SendAsync(Post("dup-key-1", new { item = "book" }));
            var second = await client.SendAsync(Post("dup-key-1", new { item = "book" }));

            first.StatusCode.Should().Be(HttpStatusCode.OK);
            second.StatusCode.Should().Be(HttpStatusCode.OK);

            // Handler executed exactly once (SC-001, SC-002).
            counter.Count.Should().Be(1);

            // Replay returns the identical stored body (SC-003).
            var firstBody = await first.Content.ReadFromJsonAsync<OrderResponse>();
            var secondBody = await second.Content.ReadFromJsonAsync<OrderResponse>();
            secondBody.Should().BeEquivalentTo(firstBody);
            firstBody!.Sequence.Should().Be(1);
        }
    }
}
