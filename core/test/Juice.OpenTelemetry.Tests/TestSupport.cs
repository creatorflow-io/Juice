using System.Collections;
using System.Diagnostics;
using Juice.Messaging.Policies;
using OpenTelemetry.Metrics;

// the OpenTelemetry providers listen process wide: one test at a time keeps each test's exported items its own
[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace Juice.OpenTelemetry.Tests
{
    internal static class TestSources
    {
        public static readonly ActivitySource Root = new("Juice.OpenTelemetry.Tests");
    }

    /// <summary>
    /// Exporter target that background dispatch can add to while the test reads it.
    /// </summary>
    internal sealed class LockedList<T> : ICollection<T>
    {
        private readonly List<T> _items = new();

        public int Count { get { lock (_items) return _items.Count; } }
        public bool IsReadOnly => false;
        public void Add(T item) { lock (_items) _items.Add(item); }
        public void Clear() { lock (_items) _items.Clear(); }
        public bool Contains(T item) { lock (_items) return _items.Contains(item); }
        public void CopyTo(T[] array, int arrayIndex) { lock (_items) _items.CopyTo(array, arrayIndex); }
        public bool Remove(T item) { lock (_items) return _items.Remove(item); }
        public List<T> Snapshot() { lock (_items) return _items.ToList(); }
        public IEnumerator<T> GetEnumerator() => Snapshot().GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    /// <summary>
    /// Reads exported metric points: sums of counters and counts of histograms whose tags include <c>key=value</c>.
    /// </summary>
    internal static class MetricValues
    {
        public static long Sum(IEnumerable<Metric> metrics, string name, string key, string value)
            => Aggregate(metrics, name, key, value, (in MetricPoint p) => p.GetSumLong());

        public static long Count(IEnumerable<Metric> metrics, string name, string key, string value)
            => Aggregate(metrics, name, key, value, (in MetricPoint p) => p.GetHistogramCount());

        private delegate long PointValue(in MetricPoint point);

        private static long Aggregate(IEnumerable<Metric> metrics, string name, string key, string value, PointValue read)
        {
            long total = 0;
            foreach (var metric in metrics.Where(m => m.Name == name))
            {
                foreach (ref readonly var point in metric.GetMetricPoints())
                {
                    foreach (var tag in point.Tags)
                    {
                        if (tag.Key == key && Equals(tag.Value, value))
                        {
                            total += read(point);
                            break;
                        }
                    }
                }
            }
            return total;
        }
    }

    internal sealed class FixedRoutePolicy(string publisherKey, string destination) : IMessagePublishingPolicy
    {
        public ValueTask<IReadOnlyCollection<PublishRoute>> ResolveAsync(PolicyResolveContext context)
        {
            IReadOnlyCollection<PublishRoute> routes = [new PublishRoute(publisherKey, destination)];
            return ValueTask.FromResult(routes);
        }
    }
}
