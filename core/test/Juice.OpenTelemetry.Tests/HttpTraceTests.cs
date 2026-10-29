using System.Diagnostics;
using FluentAssertions;
using Juice.MediatR;
using Juice.Messaging;
using Juice.Messaging.Policies;
using Juice.XUnit;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace Juice.OpenTelemetry.Tests
{
    /// <summary>
    /// The first milestone of the OpenTelemetry integration: one connected trace from the HTTP request through the
    /// mediator and a published message to its handler, with the Juice context on the spans.
    /// </summary>
    public class HttpTraceTests
    {
        private readonly ITestOutputHelper _output;

        public HttpTraceTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public async Task Http_request_should_produce_one_connected_trace_Async()
        {
            var spans = new LockedList<Activity>();
            using var host = await StartHostAsync(spans);
            var client = host.GetTestClient();

            // the caller's span, as an upstream service would send it
            var traceId = ActivityTraceId.CreateRandom();
            var callerSpanId = ActivitySpanId.CreateRandom();
            using var request = new HttpRequestMessage(HttpMethod.Post, "/orders");
            request.Headers.Add("traceparent", $"00-{traceId}-{callerSpanId}-01");
            request.Headers.Add("x-correlation-id", "corr-http");

            using var response = await client.SendAsync(request);
            response.EnsureSuccessStatusCode();

            var handle = await WaitForAsync(spans, traceId, "handle " + nameof(OrderPlacedHandler));
            // the server span stops once the response completes, possibly after the background handler span
            await WaitForAsync(spans, traceId, "Microsoft.AspNetCore.Hosting.HttpRequestIn");
            var trace = spans.Snapshot().Where(a => a.TraceId == traceId).ToList();
            _output.WriteLine(string.Join(Environment.NewLine, trace.Select(a => $"{a.Kind,-9} {a.OperationName} {a.SpanId} <- {a.ParentSpanId}")));

            var server = trace.Should().ContainSingle(a => a.Kind == ActivityKind.Server, "Juice must not add a second HTTP span").Subject;
            server.Source.Name.Should().NotBe("Juice");
            server.ParentSpanId.Should().Be(callerSpanId, "the request continues the caller's trace");
            server.GetTagItem("juice.correlation_id").Should().Be("corr-http");
            server.GetTagItem("juice.source").Should().Be("orders-api");
            server.GetTagItem("juice.execution_id").Should().NotBeNull();

            var mediator = trace.Should().ContainSingle(a => a.OperationName == "mediator " + nameof(PlaceOrder)).Subject;
            mediator.ParentSpanId.Should().Be(server.SpanId);
            mediator.GetTagItem("juice.correlation_id").Should().Be("corr-http");

            var send = trace.Should().ContainSingle(a => a.OperationName == "send " + nameof(OrderPlaced)).Subject;
            send.Kind.Should().Be(ActivityKind.Producer);
            send.ParentSpanId.Should().Be(mediator.SpanId);

            var process = trace.Should().ContainSingle(a => a.OperationName == "process " + nameof(OrderPlaced)).Subject;
            process.Kind.Should().Be(ActivityKind.Consumer);
            process.ParentSpanId.Should().Be(send.SpanId);
            process.GetTagItem("juice.correlation_id").Should().Be("corr-http", "the correlation id travels with the message");
            process.GetTagItem("juice.causation_id").Should().Be(server.GetTagItem("juice.execution_id"),
                "the consumer is caused by the request execution");

            handle.ParentSpanId.Should().Be(process.SpanId);
        }

        [Fact]
        public async Task Http_request_should_record_framework_metrics_Async()
        {
            var spans = new LockedList<Activity>();
            var metrics = new LockedList<Metric>();
            using var host = await StartHostAsync(spans, metrics);
            var meterProvider = host.Services.GetRequiredService<MeterProvider>();
            List<Metric> Collect()
            {
                // cumulative temporality: the latest export holds the totals
                metrics.Clear();
                meterProvider.ForceFlush();
                return metrics.Snapshot();
            }

            using var response = await host.GetTestClient().PostAsync("/orders", null);
            response.EnsureSuccessStatusCode();
            await Waiter.WaitAsync(() => MetricValues.Sum(Collect(), "juice.messaging.consumed", "juice.message_type", nameof(OrderPlaced)) == 1,
                TimeSpan.FromSeconds(5));

            var exported = Collect();
            MetricValues.Sum(exported, "juice.mediator.requests", "juice.operation", nameof(PlaceOrder)).Should().Be(1);
            MetricValues.Sum(exported, "juice.messaging.published", "messaging.system", "local-channel").Should().Be(1);
            MetricValues.Sum(exported, "juice.messaging.consumed", "juice.dispatch.result", "success").Should().Be(1);
            MetricValues.Count(exported, "juice.messaging.process.duration", "juice.message_type", nameof(OrderPlaced)).Should().Be(1);
            MetricValues.Sum(exported, "juice.messaging.errors", "juice.message_type", nameof(OrderPlaced)).Should().Be(0);
            exported.Where(m => m.Name.StartsWith("juice.")).SelectMany(m => Tags(m)).Should()
                .NotContain(k => k == "messaging.message.id" || k == "juice.correlation_id", "ids are never metric dimensions");
        }

        private static IEnumerable<string> Tags(Metric metric)
        {
            var keys = new HashSet<string>();
            foreach (ref readonly var point in metric.GetMetricPoints())
            {
                foreach (var tag in point.Tags)
                {
                    keys.Add(tag.Key);
                }
            }
            return keys;
        }

        private async Task<IHost> StartHostAsync(ICollection<Activity> spans, ICollection<Metric>? metrics = null)
        {
            return await new HostBuilder()
                .ConfigureWebHost(web => web
                    .UseTestServer()
                    .ConfigureServices(services =>
                    {
                        services.AddLogging(b => b.AddTestOutputLogger(_output));
                        services.AddRouting();
                        services.AddOpenTelemetry()
                            .WithTracing(tracing => tracing
                                .AddAspNetCoreInstrumentation()
                                .AddJuiceInstrumentation()
                                .AddInMemoryExporter(spans))
                            .WithMetrics(meters => meters
                                .AddJuiceInstrumentation()
                                .AddInMemoryExporter(metrics ?? new List<Metric>()));
                        services.AddMediatR(b => b.RegisterServicesFromAssemblyContaining<HttpTraceTests>(includeNonPublicTypes: false));
                        services.AddJuiceOpenTelemetry();

                        var messaging = services.AddMessaging();
                        messaging.AddLocalChannel();
                        messaging.AddIdempotencyInMemory();
                        messaging.AddDefaultMessageService(_ => { });
                        services.AddSingleton<IMessagePublishingPolicy>(new FixedRoutePolicy("local-channel", string.Empty));
                        services.AddTransient<IIntegrationEventHandler<OrderPlaced>, OrderPlacedHandler>();
                    })
                    .Configure(app =>
                    {
                        app.UseMessageContext("orders-api");
                        app.UseRouting();
                        app.UseEndpoints(endpoints => endpoints.MapPost("/orders", async (IMediator mediator) =>
                        {
                            await mediator.Send(new PlaceOrder());
                            return Results.Accepted();
                        }));
                    }))
                .StartAsync();
        }

        private static async Task<Activity> WaitForAsync(LockedList<Activity> spans, ActivityTraceId traceId, string name)
        {
            Activity? Find() => spans.Snapshot().FirstOrDefault(a => a.TraceId == traceId && a.OperationName == name);
            await Waiter.WaitAsync(() => Find() != null, TimeSpan.FromSeconds(5));
            return Find() ?? throw new TimeoutException($"No span '{name}'. Exported: "
                + string.Join(", ", spans.Snapshot().Where(a => a.TraceId == traceId).Select(a => a.OperationName)));
        }
    }

    public sealed record PlaceOrder : MessageBase, IRequest;

    public sealed class PlaceOrderHandler(IMessageService messages) : IRequestHandler<PlaceOrder>
    {
        public async ValueTask Handle(PlaceOrder request, CancellationToken cancellationToken)
            => await messages.PublishAsync(new OrderPlaced(), cancellationToken);
    }

    public sealed record OrderPlaced : IntegrationEvent;

    public sealed class OrderPlacedHandler : IIntegrationEventHandler<OrderPlaced>
    {
        public Task HandleAsync(OrderPlaced @event) => Task.CompletedTask;
    }
}
