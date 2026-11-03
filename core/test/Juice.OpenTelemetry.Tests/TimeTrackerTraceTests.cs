using System.Diagnostics;
using FluentAssertions;
using Juice.Measurement.Internal;
using OpenTelemetry;
using OpenTelemetry.Trace;

namespace Juice.OpenTelemetry.Tests
{
    public class TimeTrackerTraceTests
    {
        private readonly ITestOutputHelper _output;

        public TimeTrackerTraceTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public void Juice_instrumentation_should_collect_time_tracker_scopes()
        {
            var spans = new LockedList<Activity>();
            using var tracer = Sdk.CreateTracerProviderBuilder()
                .AddSource(TestSources.Root.Name)
                .AddJuiceInstrumentation()
                .AddInMemoryExporter(spans)
                .Build()!;

            string records;
            using (var root = TestSources.Root.StartActivity("test")!)
            using (var tracker = new TimeTracker())
            {
                using (tracker.BeginScope("Saving integration events"))
                {
                    tracker.Checkpoint("SaveChanges");
                }
                records = tracker.ToString();
            }

            _output.WriteLine(records);
            _output.WriteLine("Exported spans:");
            foreach (var a in spans.Snapshot().OrderBy(a => a.StartTimeUtc))
            {
                _output.WriteLine($"  [{a.Source.Name}] {a.OperationName} [{a.SpanId}] parent={a.ParentSpanId} duration={a.Duration.TotalMilliseconds:F1}ms"
                    + string.Concat(a.Events.Select(e => $" event={e.Name}")));
            }

            var span = spans.Snapshot().Should().ContainSingle(a => a.OperationName == "Saving integration events").Subject;
            span.Source.Name.Should().Be(Juice.Measurement.TimeTrackerActivitySource.Name);
            span.Events.Select(e => e.Name).Should().Equal("SaveChanges");
        }
    }
}
