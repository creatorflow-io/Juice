using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Juice.AspNetCore.Idempotency.Tests
{
    // US3: reusing a key with a materially different payload is rejected (422) and applies no effect.
    public class PayloadConflictTests
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
        public async Task Same_key_different_payload_returns_422_and_no_second_execution_Async()
        {
            using var host = await IdempotencyTestHost.CreateAsync();
            var client = host.GetTestClient();
            var counter = host.Services.GetRequiredService<CallCounter>();

            var first = await client.SendAsync(Post("conflict-key-1", new { item = "book" }));
            first.StatusCode.Should().Be(HttpStatusCode.OK);

            var second = await client.SendAsync(Post("conflict-key-1", new { item = "pen" }));

            ((int)second.StatusCode).Should().Be(422);
            counter.Count.Should().Be(1); // conflicting retry applied no effect
        }
    }
}
