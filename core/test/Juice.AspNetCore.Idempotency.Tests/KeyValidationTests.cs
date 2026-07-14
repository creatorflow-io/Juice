using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.TestHost;

namespace Juice.AspNetCore.Idempotency.Tests
{
    // US3: missing/invalid keys get clear, distinct rejections and apply no effect.
    public class KeyValidationTests
    {
        [Fact]
        public async Task Missing_header_returns_400_Async()
        {
            using var host = await IdempotencyTestHost.CreateAsync();
            var client = host.GetTestClient();

            var res = await client.PostAsJsonAsync("/orders", new { item = "book" });

            res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }

        [Fact]
        public async Task Over_length_key_returns_400_Async()
        {
            using var host = await IdempotencyTestHost.CreateAsync();
            var client = host.GetTestClient();

            var req = new HttpRequestMessage(HttpMethod.Post, "/orders")
            {
                Content = JsonContent.Create(new { item = "book" })
            };
            req.Headers.Add("Idempotency-Key", new string('k', 129)); // MaxKeyLength default = 128

            var res = await client.SendAsync(req);

            res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }
    }
}
