using System.Diagnostics;
using FluentAssertions;
using Juice.MediatR;
using Juice.XUnit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace Juice.OpenTelemetry.Tests
{
    public class MediatorInstrumentationTests
    {
        private readonly ITestOutputHelper _output;

        public MediatorInstrumentationTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        [InitializeMessageContext]
        public async Task Request_should_be_traced_under_the_current_span_Async()
        {
            var spans = new LockedList<Activity>();
            using var tracer = Tracer(spans);
            using var provider = BuildServices();
            var mediator = provider.GetRequiredService<IMediator>();

            using var root = TestSources.Root.StartActivity("test")!;
            (await mediator.Send(new PingRequest("secret-text"))).Should().Be("pong:secret-text");

            var span = spans.Snapshot().Should().ContainSingle(a => a.TraceId == root.TraceId && a.OperationName == "mediator PingRequest").Subject;
            span.ParentSpanId.Should().Be(root.SpanId);
            span.Kind.Should().Be(ActivityKind.Internal);
            span.Status.Should().Be(ActivityStatusCode.Unset, "a successful span keeps the default status");
            span.GetTagItem("juice.component").Should().Be("mediator");
            span.GetTagItem("juice.operation").Should().Be("PingRequest");
            span.GetTagItem("juice.mediator.kind").Should().Be("request");
            span.GetTagItem("juice.correlation_id").Should().Be(Juice.Messaging.MessageContext.Current.CorrelationId);
            span.TagObjects.Should().NotContain(t => (t.Value as string ?? "").Contains("secret-text"), "request values are never recorded");
        }

        [Fact]
        public async Task Tracing_behavior_should_wrap_every_other_behavior_Async()
        {
            var spans = new LockedList<Activity>();
            using var tracer = Tracer(spans);
            var observer = new BehaviorObserver();
            using var provider = BuildServices(services =>
            {
                services.AddSingleton(observer);
                // the earliest a user behavior can sit after the tracing behavior
                services.AddSingleton<IPipelineBehavior<PingRequest, string>, EarlyBehavior>();
            });

            await provider.GetRequiredService<IMediator>().Send(new PingRequest("order"));

            observer.CurrentActivity.Should().Be("mediator PingRequest");
        }

        [Fact]
        public async Task Failing_request_should_record_the_error_Async()
        {
            var spans = new LockedList<Activity>();
            var metrics = new List<Metric>();
            using var tracer = Tracer(spans);
            using var meter = Meter(metrics);
            using var provider = BuildServices();

            using var root = TestSources.Root.StartActivity("test")!;
            var act = async () => await provider.GetRequiredService<IMediator>().Send(new FailingRequest());
            await act.Should().ThrowAsync<InvalidOperationException>();

            var span = spans.Snapshot().Should().ContainSingle(a => a.TraceId == root.TraceId && a.OperationName == "mediator FailingRequest").Subject;
            span.Status.Should().Be(ActivityStatusCode.Error);
            span.StatusDescription.Should().Be("request failed (test)");
            span.Events.Should().ContainSingle(e => e.Name == "exception");

            meter.ForceFlush();
            MetricValues.Sum(metrics, "juice.mediator.errors", "juice.operation", "FailingRequest").Should().Be(1);
            MetricValues.Sum(metrics, "juice.mediator.requests", "juice.operation", "FailingRequest").Should().Be(1);
            MetricValues.Count(metrics, "juice.mediator.request.duration", "juice.operation", "FailingRequest").Should().Be(1);
        }

        [Fact]
        public async Task Requests_should_be_counted_by_type_Async()
        {
            var metrics = new List<Metric>();
            using var meter = Meter(metrics);
            using var provider = BuildServices();
            var mediator = provider.GetRequiredService<IMediator>();

            await mediator.Send(new PingRequest("1"));
            await mediator.Send(new PingRequest("2"));

            meter.ForceFlush();
            MetricValues.Sum(metrics, "juice.mediator.requests", "juice.operation", "PingRequest").Should().Be(2);
            MetricValues.Sum(metrics, "juice.mediator.errors", "juice.operation", "PingRequest").Should().Be(0);
            MetricValues.Count(metrics, "juice.mediator.request.duration", "juice.operation", "PingRequest").Should().Be(2);
            metrics.Single(m => m.Name == "juice.mediator.request.duration").Unit.Should().Be("s");
        }

        [Fact]
        public async Task Notification_should_be_traced_Async()
        {
            var spans = new LockedList<Activity>();
            using var tracer = Tracer(spans);
            using var provider = BuildServices();

            using var root = TestSources.Root.StartActivity("test")!;
            await provider.GetRequiredService<IMediator>().Publish(new SomethingHappened());

            spans.Snapshot().Should().ContainSingle(a => a.TraceId == root.TraceId && a.OperationName == "mediator SomethingHappened")
                .Which.GetTagItem("juice.mediator.kind").Should().Be("notification");
        }

        [Fact]
        public async Task Options_should_skip_filtered_requests_and_notifications_Async()
        {
            var spans = new LockedList<Activity>();
            using var tracer = Tracer(spans);
            using var provider = BuildServices(configure: options =>
            {
                options.Filter = type => type != typeof(PingRequest);
                options.EnableNotificationInstrumentation = false;
            });
            var mediator = provider.GetRequiredService<IMediator>();

            using var root = TestSources.Root.StartActivity("test")!;
            await mediator.Send(new PingRequest("skip"));
            await mediator.Publish(new SomethingHappened());
            await mediator.Send(new VoidRequest());

            spans.Snapshot().Where(a => a.TraceId == root.TraceId).Select(a => a.OperationName)
                .Should().BeEquivalentTo("mediator VoidRequest");
        }

        [Fact]
        public async Task Mediator_should_work_without_a_listener_Async()
        {
            using var provider = BuildServices();

            (await provider.GetRequiredService<IMediator>().Send(new PingRequest("quiet"))).Should().Be("pong:quiet");
        }

        // ─── helpers ───────────────────────────────────────────────────────────

        private ServiceProvider BuildServices(Action<IServiceCollection>? services = null,
            Action<JuiceOpenTelemetryOptions>? configure = null)
        {
            var collection = new ServiceCollection();
            collection.AddLogging(b => b.AddTestOutputLogger(_output));
            collection.AddMediatR(b => b.RegisterServicesFromAssemblyContaining<MediatorInstrumentationTests>(includeNonPublicTypes: false));
            collection.AddJuiceOpenTelemetry(configure);
            services?.Invoke(collection);
            return collection.BuildServiceProvider();
        }

        private static TracerProvider Tracer(ICollection<Activity> spans)
            => Sdk.CreateTracerProviderBuilder()
                .AddSource(TestSources.Root.Name)
                .AddJuiceInstrumentation()
                .AddInMemoryExporter(spans)
                .Build()!;

        private static MeterProvider Meter(ICollection<Metric> metrics)
            => Sdk.CreateMeterProviderBuilder()
                .AddJuiceInstrumentation()
                .AddInMemoryExporter(metrics)
                .Build()!;
    }

    public sealed record PingRequest(string Text) : MessageBase, IRequest<string>;

    public sealed class PingHandler : IRequestHandler<PingRequest, string>
    {
        public ValueTask<string> Handle(PingRequest request, CancellationToken cancellationToken)
            => ValueTask.FromResult("pong:" + request.Text);
    }

    public sealed record VoidRequest : MessageBase, IRequest;

    public sealed class VoidHandler : IRequestHandler<VoidRequest>
    {
        public ValueTask Handle(VoidRequest request, CancellationToken cancellationToken) => default;
    }

    public sealed record FailingRequest : MessageBase, IRequest;

    public sealed class FailingHandler : IRequestHandler<FailingRequest>
    {
        public ValueTask Handle(FailingRequest request, CancellationToken cancellationToken)
            => throw new InvalidOperationException("request failed (test)");
    }

    public sealed record SomethingHappened : MessageBase, INotification;

    public sealed class SomethingHappenedHandler : INotificationHandler<SomethingHappened>
    {
        public ValueTask Handle(SomethingHappened notification, CancellationToken cancellationToken = default) => default;
    }

    internal sealed class BehaviorObserver
    {
        public string? CurrentActivity { get; set; }
    }

    internal sealed class EarlyBehavior(BehaviorObserver observer) : IPipelineBehavior<PingRequest, string>
    {
        public int Order => int.MinValue + 1;

        public ValueTask<string> Handle(PingRequest request, RequestHandlerDelegate<PingRequest, string> next, CancellationToken cancellationToken)
        {
            observer.CurrentActivity = Activity.Current?.OperationName;
            return next.Invoke(request, cancellationToken);
        }
    }
}
