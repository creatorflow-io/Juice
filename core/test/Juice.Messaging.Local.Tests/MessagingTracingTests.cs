using System.Diagnostics;
using System.Text;
using FluentAssertions;
using Juice.EventBus.Publishing;
using Juice.Messaging.Diagnostics;
using Juice.Messaging.Local.Internal;
using Juice.Messaging.Outbox;
using Juice.Messaging.Policies;
using Juice.XUnit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Juice.Messaging.Local.Tests
{
    /// <summary>
    /// W3C trace propagation through the outbox and the in-process transports, recorded with a plain
    /// <see cref="ActivityListener"/> (no OpenTelemetry SDK).
    /// </summary>
    public class MessagingTracingTests
    {
        private readonly ITestOutputHelper _output;

        public MessagingTracingTests(ITestOutputHelper output)
        {
            _output = output;
        }

        // ─── propagation helper ────────────────────────────────────────────────

        [Fact]
        public void Inject_and_extract_should_round_trip_the_trace_context()
        {
            using var collector = new ActivityCollector();
            using var root = collector.StartRoot();
            root.TraceStateString = "juice=1";

            var headers = new Dictionary<string, object?>();
            MessagingActivity.Inject(headers);

            headers[MessagingActivity.TraceParentHeader].Should().Be(root.Id);
            var context = MessagingActivity.Extract(headers);
            context.TraceId.Should().Be(root.TraceId);
            context.SpanId.Should().Be(root.SpanId);
            context.TraceState.Should().Be("juice=1");
        }

        [Fact]
        public void Extract_should_read_utf8_header_values()
        {
            using var collector = new ActivityCollector();
            using var root = collector.StartRoot();

            // AMQP delivers string headers as bytes
            var headers = new Dictionary<string, object?>
            {
                [MessagingActivity.TraceParentHeader] = Encoding.UTF8.GetBytes(root.Id!)
            };

            MessagingActivity.Extract(headers).SpanId.Should().Be(root.SpanId);
        }

        [Fact]
        public void Inject_should_keep_the_context_a_message_was_created_in()
        {
            using var collector = new ActivityCollector();
            using var root = collector.StartRoot();
            var created = "00-0af7651916cd43dd8448eb211c80319c-b7ad6b7169203331-01";
            var headers = new Dictionary<string, object?> { [MessagingActivity.TraceParentHeader] = created };

            MessagingActivity.Inject(headers);

            headers[MessagingActivity.TraceParentHeader].Should().Be(created);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("not-a-traceparent")]
        public void Extract_should_ignore_a_missing_or_invalid_context(string? traceParent)
        {
            var headers = new Dictionary<string, object?>();
            if (traceParent != null)
            {
                headers[MessagingActivity.TraceParentHeader] = traceParent;
            }

            MessagingActivity.Extract(headers).Should().Be(default(ActivityContext));
            MessagingActivity.Extract(null).Should().Be(default(ActivityContext));
        }

        // ─── outbox ────────────────────────────────────────────────────────────

        [Fact]
        [InitializeMessageContext]
        public async Task Outbox_should_store_the_create_span_context_with_the_message_Async()
        {
            using var collector = new ActivityCollector();
            var repository = new CapturingOutboxRepository();
            var services = new ServiceCollection();
            services.AddLogging(b => b.AddTestOutputLogger(_output));
            services.AddMessaging().AddOutboxCore();
            services.AddSingleton<IMessagePublishingPolicy>(new FixedRoutePolicy("rabbitmq", "orders"));
            services.AddSingleton<IOutboxRepository<TracingTestContext>>(repository);
            using var provider = services.BuildServiceProvider();
            using var scope = provider.CreateScope();
            var outbox = scope.ServiceProvider.GetRequiredService<IOutboxService<TracingTestContext>>();

            using var root = collector.StartRoot();
            var message = new TracingTestEvent();
            await outbox.AddEventAsync(message);
            await outbox.SaveEventsAsync(null, default);

            var create = collector.InTrace(root.TraceId).Should().ContainSingle(a => a.OperationName == "create TracingTestEvent").Subject;
            create.Kind.Should().Be(ActivityKind.Producer);
            create.ParentSpanId.Should().Be(root.SpanId);
            create.GetTagItem(MessagingActivity.Tags.MessageId).Should().Be(message.MessageId.ToString());
            create.GetTagItem(MessagingActivity.Tags.CorrelationId).Should().Be(MessageContext.Current.CorrelationId);

            var stored = repository.Events.Should().ContainSingle().Subject;
            stored.Headers[MessagingActivity.TraceParentHeader].Should().Be(create.Id);
            stored.Headers["x-correlation-id"].Should().Be(MessageContext.Current.CorrelationId, "the Juice context headers stay as they are");
        }

        // ─── "local" route (outbox delivery to in-process handlers) ────────────

        [Fact]
        [InitializeMessageContext]
        public async Task Local_delivery_should_continue_the_stored_trace_Async()
        {
            using var collector = new ActivityCollector();
            var created = new ActivityContext(ActivityTraceId.CreateRandom(), ActivitySpanId.CreateRandom(), ActivityTraceFlags.Recorded);
            using var provider = BuildLocalServices(svc =>
                svc.AddTransient<IIntegrationEventHandler<TracingTestEvent>, RecordingHandler>());
            var (publisher, payload, context) = CreateLocalDelivery(provider, created);

            await publisher.PublishAsync(payload, context);

            var trace = collector.InTrace(created.TraceId);
            var process = trace.Should().ContainSingle(a => a.OperationName == "process TracingTestEvent").Subject;
            process.Kind.Should().Be(ActivityKind.Consumer);
            process.ParentSpanId.Should().Be(created.SpanId);
            process.GetTagItem("messaging.system").Should().Be("local");
            process.GetTagItem(MessagingActivity.Tags.DispatchResult).Should().Be("success");
            process.GetTagItem(MessagingActivity.Tags.CorrelationId).Should().Be("corr-trace");

            var handle = trace.Should().ContainSingle(a => a.OperationName == "handle " + nameof(RecordingHandler)).Subject;
            handle.ParentSpanId.Should().Be(process.SpanId);
            handle.Status.Should().Be(ActivityStatusCode.Unset);
        }

        [Fact]
        [InitializeMessageContext]
        public async Task Local_delivery_should_nest_under_the_current_span_of_the_same_trace_Async()
        {
            using var collector = new ActivityCollector();
            using var provider = BuildLocalServices(svc =>
                svc.AddTransient<IIntegrationEventHandler<TracingTestEvent>, RecordingHandler>());
            using var root = collector.StartRoot("deliver local");
            var (publisher, payload, context) = CreateLocalDelivery(provider, root.Context);

            await publisher.PublishAsync(payload, context);

            collector.InTrace(root.TraceId).Should().ContainSingle(a => a.OperationName == "process TracingTestEvent")
                .Which.ParentSpanId.Should().Be(root.SpanId);
        }

        [Fact]
        [InitializeMessageContext]
        public async Task Failing_handler_should_mark_the_spans_as_errors_Async()
        {
            using var collector = new ActivityCollector();
            var created = new ActivityContext(ActivityTraceId.CreateRandom(), ActivitySpanId.CreateRandom(), ActivityTraceFlags.Recorded);
            using var provider = BuildLocalServices(svc =>
                svc.AddTransient<IIntegrationEventHandler<TracingTestEvent>, ThrowingHandler>());
            var (publisher, payload, context) = CreateLocalDelivery(provider, created);

            var act = async () => await publisher.PublishAsync(payload, context);
            await act.Should().ThrowAsync<InvalidOperationException>();

            var trace = collector.InTrace(created.TraceId);
            var handle = trace.Should().ContainSingle(a => a.OperationName == "handle " + nameof(ThrowingHandler)).Subject;
            handle.Status.Should().Be(ActivityStatusCode.Error);
            handle.Events.Should().ContainSingle(e => e.Name == "exception")
                .Which.Tags.Should().Contain(new KeyValuePair<string, object?>("exception.message", "handler failed (test)"));

            var process = trace.Should().ContainSingle(a => a.OperationName == "process TracingTestEvent").Subject;
            process.Status.Should().Be(ActivityStatusCode.Error);
            process.GetTagItem(MessagingActivity.Tags.DispatchResult).Should().Be("failure");
        }

        // ─── "local-channel" route (in-memory queue, background dispatch) ──────

        [Fact]
        [InitializeMessageContext]
        public async Task Local_channel_should_continue_the_publisher_trace_Async()
        {
            using var collector = new ActivityCollector();
            using var provider = BuildLocalServices(svc =>
            {
                svc.AddTransient<IIntegrationEventHandler<TracingTestEvent>, RecordingHandler>();
                svc.AddSingleton<IMessagePublishingPolicy>(new FixedRoutePolicy("local-channel", string.Empty));
            }, localChannel: true);
            var hostedServices = provider.GetServices<IHostedService>().ToList();
            foreach (var hs in hostedServices) await hs.StartAsync(default);
            try
            {
                using var root = collector.StartRoot();
                using (var scope = provider.CreateScope())
                {
                    await scope.ServiceProvider.GetRequiredService<IMessageService>().PublishAsync(new TracingTestEvent());
                }

                var send = await collector.WaitForAsync(root.TraceId, "send TracingTestEvent");
                var process = await collector.WaitForAsync(root.TraceId, "process TracingTestEvent");
                var handle = await collector.WaitForAsync(root.TraceId, "handle " + nameof(RecordingHandler));

                send.Kind.Should().Be(ActivityKind.Producer);
                send.ParentSpanId.Should().Be(root.SpanId);
                process.ParentSpanId.Should().Be(send.SpanId, "the reader loop continues the context captured at enqueue");
                handle.ParentSpanId.Should().Be(process.SpanId);
                process.GetTagItem(MessagingActivity.Tags.CausationId).Should().Be(MessageContext.Current.ExecutionId,
                    "the Juice causal chain is recorded next to the trace, not replaced by it");
            }
            finally
            {
                foreach (var hs in hostedServices) await hs.StopAsync(default);
            }
        }

        // ─── helpers ───────────────────────────────────────────────────────────

        private ServiceProvider BuildLocalServices(Action<IServiceCollection> configure, bool localChannel = false)
        {
            var services = new ServiceCollection();
            services.AddLogging(b => b.AddTestOutputLogger(_output));
            var messaging = services.AddMessaging();
            messaging.AddIdempotencyInMemory();
            if (localChannel)
            {
                messaging.AddLocalChannel();
                messaging.AddDefaultMessageService(_ => { });
            }
            services.AddMediatR();
            configure(services);
            return services.BuildServiceProvider();
        }

        private static (LocalTransportPublisher Publisher, byte[] Payload, PublishContext Context) CreateLocalDelivery(
            IServiceProvider provider, ActivityContext created)
        {
            var scope = provider.CreateScope().ServiceProvider;
            var publisher = ActivatorUtilities.CreateInstance<LocalTransportPublisher>(scope);
            var message = new TracingTestEvent();
            var payload = scope.GetRequiredService<IMessageSerializer>().SerializeToUtf8Bytes(message);
            var headers = new Dictionary<string, object?>
            {
                ["x-message-type"] = nameof(TracingTestEvent),
                ["x-correlation-id"] = "corr-trace",
                ["x-source"] = "tracing-test",
                [MessagingActivity.TraceParentHeader] = $"00-{created.TraceId}-{created.SpanId}-01"
            };
            return (publisher, payload, new PublishContext(message.MessageId.ToString(), Headers: headers));
        }

        private sealed class TracingTestContext { }

        private sealed record TracingTestEvent : IntegrationEvent;

        private sealed class RecordingHandler : IIntegrationEventHandler<TracingTestEvent>
        {
            public Task HandleAsync(TracingTestEvent @event) => Task.CompletedTask;
        }

        private sealed class ThrowingHandler : IIntegrationEventHandler<TracingTestEvent>
        {
            public Task HandleAsync(TracingTestEvent @event) => throw new InvalidOperationException("handler failed (test)");
        }

        private sealed class FixedRoutePolicy(string publisherKey, string destination) : IMessagePublishingPolicy
        {
            public ValueTask<IReadOnlyCollection<PublishRoute>> ResolveAsync(PolicyResolveContext context)
            {
                IReadOnlyCollection<PublishRoute> routes = [new PublishRoute(publisherKey, destination)];
                return ValueTask.FromResult(routes);
            }
        }

        private sealed class CapturingOutboxRepository : IOutboxRepository<TracingTestContext>
        {
            public List<OutboxEvent> Events { get; } = new();

            public ValueTask SaveEventsAsync(OutboxEvent[] @event, CancellationToken cancellationToken = default)
            {
                Events.AddRange(@event);
                return default;
            }

            public ValueTask MarkAsPublishedAsync(Guid deliveryId, CancellationToken cancellationToken = default) => default;
            public ValueTask<int> MarkAsInProgressAsync(Guid deliveryId, CancellationToken cancellationToken = default) => new(1);
            public ValueTask MarkAsFailedAsync(Guid deliveryId, string error, DateTimeOffset? nextAttempt, CancellationToken cancellationToken = default) => default;
            public ValueTask MarkAsSkippedAsync(Guid deliveryId, string reason, CancellationToken cancellationToken = default) => default;
            public void Dispose() { }
        }
    }
}
