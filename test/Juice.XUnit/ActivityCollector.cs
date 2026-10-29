using System.Collections.Concurrent;
using System.Diagnostics;
using Juice.Messaging.Diagnostics;

namespace Juice.XUnit
{
    /// <summary>
    /// Records the activities of <see cref="JuiceActivitySource"/> (and of the collector's own source) without an
    /// OpenTelemetry SDK. Listeners are process wide, so tests read only the activities of their own trace:
    /// start the test inside <see cref="StartRoot"/> or filter with <see cref="InTrace"/>.
    /// </summary>
    public sealed class ActivityCollector : IDisposable
    {
        public const string TestSourceName = "Juice.XUnit";

        private static readonly ActivitySource _testSource = new(TestSourceName);

        private readonly ActivityListener _listener;
        private readonly ConcurrentQueue<Activity> _stopped = new();

        public ActivityCollector()
        {
            _listener = new ActivityListener
            {
                ShouldListenTo = source => source.Name == JuiceActivitySource.Name || source.Name == TestSourceName,
                Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
                ActivityStopped = _stopped.Enqueue
            };
            ActivitySource.AddActivityListener(_listener);
        }

        /// <summary>
        /// Starts a root activity: everything the test does under it shares its trace id.
        /// </summary>
        public Activity StartRoot(string name = "test")
            => _testSource.StartActivity(name, ActivityKind.Internal)
                ?? throw new InvalidOperationException("The collector is not listening.");

        /// <summary>
        /// Stopped activities of <paramref name="traceId"/>, in stop order.
        /// </summary>
        public IReadOnlyList<Activity> InTrace(ActivityTraceId traceId)
            => _stopped.Where(a => a.TraceId == traceId).ToList();

        /// <summary>
        /// Waits until an activity of <paramref name="traceId"/> named <paramref name="operationName"/> has stopped.
        /// </summary>
        public async Task<Activity> WaitForAsync(ActivityTraceId traceId, string operationName, TimeSpan? timeout = default)
        {
            Activity? Find() => _stopped.FirstOrDefault(a => a.TraceId == traceId && a.OperationName == operationName);
            await Waiter.WaitAsync(() => Find() != null, timeout ?? TimeSpan.FromSeconds(5));
            return Find() ?? throw new TimeoutException($"No stopped activity '{operationName}' in trace {traceId}. Stopped: "
                + string.Join(", ", InTrace(traceId).Select(a => a.OperationName)));
        }

        public void Dispose() => _listener.Dispose();
    }
}
