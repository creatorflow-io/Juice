using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Juice.Measurement;
using Juice.Measurement.Internal;
using Xunit;

namespace Juice.Core.Tests
{
    public class TimeTrackerActivityTest
    {
        private readonly ITestOutputHelper _output;

        public TimeTrackerActivityTest(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public void Scopes_should_be_spans_and_checkpoints_their_events()
        {
            var spans = new ConcurrentQueue<Activity>();
            using var listener = Listen(spans);
            using var root = new Activity("request").SetIdFormat(ActivityIdFormat.W3C).Start();

            string records;
            using (var tracker = new TimeTracker())
            {
                tracker.Checkpoint("Before any scope");
                using (tracker.BeginScope("Outer", "outer.id"))
                {
                    tracker.Checkpoint("Step 1");
                    using (tracker.BeginScope("Inner"))
                    {
                        tracker.Checkpoint("Step 2");
                    }
                    Activity.Current!.OperationName.Should().Be("Outer", "ending a scope restores its parent span");
                }
                records = tracker.ToString();
            }

            Activity.Current.Should().BeSameAs(root);
            var mine = spans.Where(s => s.TraceId == root.TraceId).ToList();
            Print(records, mine);
            mine.Select(s => s.OperationName).Should().Equal("Inner", "Outer");
            var inner = mine[0];
            var outer = mine[1];
            outer.ParentSpanId.Should().Be(root.SpanId);
            inner.ParentSpanId.Should().Be(outer.SpanId);
            outer.GetTagItem(TimeTrackerActivitySource.ScopeIdTag).Should().Be("outer.id");
            inner.GetTagItem(TimeTrackerActivitySource.ScopeIdTag).Should().BeNull();
            outer.Events.Select(e => e.Name).Should().Equal("Step 1");
            inner.Events.Select(e => e.Name).Should().Equal("Step 2");
            root.Events.Should().BeEmpty("a checkpoint outside a scope only goes to the records");
        }

        [Fact]
        public async Task Parallel_branches_should_keep_their_own_scopes_Async()
        {
            var spans = new ConcurrentQueue<Activity>();
            using var listener = Listen(spans);
            using var root = new Activity("request").SetIdFormat(ActivityIdFormat.W3C).Start();
            using var tracker = new TimeTracker();

            using (tracker.BeginScope("Request"))
            {
                await Task.WhenAll(Enumerable.Range(0, 2).Select(async i =>
                {
                    using (tracker.BeginScope($"Branch {i}"))
                    {
                        await Task.Delay(20);
                        using (tracker.BeginScope($"Step {i}"))
                        {
                            await Task.Delay(20);
                            tracker.Checkpoint($"Done {i}");
                        }
                    }
                }));
                tracker.Checkpoint("After branches");
            }

            Print(tracker.ToString(), spans.Where(s => s.TraceId == root.TraceId));
            tracker.Records.OfType<ScopeEnd>().Select(e => (e.FullName, e.Depth)).Should().BeEquivalentTo(new[]
            {
                ("Request > Branch 0 > Step 0", 2), ("Request > Branch 1 > Step 1", 2),
                ("Request > Branch 0", 1), ("Request > Branch 1", 1), ("Request", 0),
            });
            tracker.Records.OfType<Checkpoint>().Single(c => c.Name == "Done 1").FullName.Should().Be("Request > Branch 1 > Step 1");
            tracker.Records.OfType<Checkpoint>().Single(c => c.Name == "After branches").FullName.Should().Be("Request");
            var mine = spans.Where(s => s.TraceId == root.TraceId).ToDictionary(s => s.OperationName);
            mine["Step 0"].ParentSpanId.Should().Be(mine["Branch 0"].SpanId);
            mine["Step 1"].ParentSpanId.Should().Be(mine["Branch 1"].SpanId);
            mine["Branch 1"].ParentSpanId.Should().Be(mine["Request"].SpanId);
        }

        private void Print(string records, IEnumerable<Activity> spans)
        {
            _output.WriteLine(records);
            _output.WriteLine("Spans:");
            foreach (var span in spans.OrderBy(s => s.StartTimeUtc))
            {
                _output.WriteLine($"  {span.OperationName} [{span.SpanId}] parent={span.ParentSpanId} duration={span.Duration.TotalMilliseconds:F1}ms"
                    + string.Concat(span.TagObjects.Select(t => $" {t.Key}={t.Value}")));
                foreach (var e in span.Events)
                {
                    _output.WriteLine($"    event {e.Name} @ {(e.Timestamp - span.StartTimeUtc).TotalMilliseconds:F1}ms");
                }
            }
        }

        private static ActivityListener Listen(ConcurrentQueue<Activity> spans)
        {
            var listener = new ActivityListener
            {
                ShouldListenTo = source => source.Name == TimeTrackerActivitySource.Name,
                Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
                ActivityStopped = spans.Enqueue,
            };
            ActivitySource.AddActivityListener(listener);
            return listener;
        }
    }
}
