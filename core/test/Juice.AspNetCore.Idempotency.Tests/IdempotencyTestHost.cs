using Juice.AspNetCore.Idempotency;
using Juice.Messaging.Idempotency;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Juice.AspNetCore.Idempotency.Tests
{
    public sealed record OrderRequest(string Item);
    public sealed record OrderResponse(int Sequence, string Item);

    /// <summary>Counts handler executions so tests can assert exactly-once.</summary>
    public sealed class CallCounter
    {
        private int _count;
        public int Count => _count;
        public int Increment() => Interlocked.Increment(ref _count);
    }

    internal static class IdempotencyTestHost
    {
        /// <summary>
        /// Build an in-process TestServer exposing a single idempotent <c>POST /orders</c> minimal-API
        /// endpoint backed by the InMemory store (registered as a singleton so state persists across
        /// requests). When <paramref name="gate"/> is set, the handler awaits it before returning, so a
        /// concurrent duplicate observes an in-progress record.
        /// </summary>
        public static async Task<IHost> CreateAsync(TaskCompletionSource<bool>? gate = null)
        {
            return await new HostBuilder()
                .ConfigureWebHost(web => web
                    .UseTestServer()
                    .ConfigureServices(services =>
                    {
                        services.AddRouting();
                        services.AddMessaging();
                        // Singleton: HTTP idempotency needs state shared across requests.
                        services.RemoveAll<IIdempotencyService>();
                        services.AddSingleton<IIdempotencyService, InMemoryIdempotencyService>();
                        services.AddApiIdempotency();
                        services.AddSingleton<CallCounter>();
                    })
                    .Configure(app =>
                    {
                        app.UseRouting();
                        app.UseEndpoints(endpoints =>
                        {
                            endpoints.MapPost("/orders", async (OrderRequest req, HttpContext ctx) =>
                            {
                                var counter = ctx.RequestServices.GetRequiredService<CallCounter>();
                                counter.Increment();
                                if (gate is not null)
                                {
                                    await gate.Task;
                                }
                                return new OrderResponse(counter.Count, req.Item);
                            })
                            .WithMetadata(new IdempotentAttribute())
                            .AddEndpointFilter<IdempotencyEndpointFilter>();
                        });
                    }))
                .StartAsync();
        }
    }
}
