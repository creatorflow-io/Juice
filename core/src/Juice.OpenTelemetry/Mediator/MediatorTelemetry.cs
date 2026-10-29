using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using Juice.Messaging.Diagnostics;

namespace Juice.OpenTelemetry.Mediator
{
    /// <summary>
    /// Spans and metrics of the mediator behaviors, on <see cref="JuiceActivitySource"/> and <see cref="JuiceMeter"/>.
    /// The operation name is the request type (a bounded set), never a value of the request.
    /// </summary>
    internal static class MediatorTelemetry
    {
        public const string OperationTag = "juice.operation";
        public const string KindTag = "juice.mediator.kind";

        private static readonly ConcurrentDictionary<Type, string> _names = new();

        private static readonly Counter<long> _requests = JuiceMeter.Meter.CreateCounter<long>(
            "juice.mediator.requests", unit: "{request}", description: "Mediator requests and notifications handled.");

        private static readonly Counter<long> _errors = JuiceMeter.Meter.CreateCounter<long>(
            "juice.mediator.errors", unit: "{error}", description: "Mediator requests and notifications that threw.");

        private static readonly Histogram<double> _duration = JuiceMeter.Meter.CreateHistogram<double>(
            "juice.mediator.request.duration", unit: "s", description: "Duration of mediator requests and notifications, behaviors included.");

        public static async ValueTask<T> RunAsync<T>(Type type, string kind, Func<ValueTask<T>> next)
        {
            var operation = GetName(type);
            var started = Stopwatch.GetTimestamp();
            using var activity = Start(operation, kind);
            try
            {
                var result = await next().ConfigureAwait(false);
                Record(operation, kind, started, error: null);
                return result;
            }
            catch (Exception ex)
            {
                MessagingActivity.SetError(activity, ex);
                Record(operation, kind, started, ex);
                throw;
            }
        }

        public static async ValueTask RunAsync(Type type, string kind, Func<ValueTask> next)
        {
            var operation = GetName(type);
            var started = Stopwatch.GetTimestamp();
            using var activity = Start(operation, kind);
            try
            {
                await next().ConfigureAwait(false);
                Record(operation, kind, started, error: null);
            }
            catch (Exception ex)
            {
                MessagingActivity.SetError(activity, ex);
                Record(operation, kind, started, ex);
                throw;
            }
        }

        private static Activity? Start(string operation, string kind)
        {
            if (!JuiceActivitySource.Source.HasListeners())
            {
                return null;
            }
            var activity = JuiceActivitySource.Source.StartActivity("mediator " + operation, ActivityKind.Internal);
            if (activity is { IsAllDataRequested: true })
            {
                activity.SetTag(MessagingActivity.Tags.Component, "mediator");
                activity.SetTag(OperationTag, operation);
                activity.SetTag(KindTag, kind);
                MessagingActivity.EnrichWithMessageContext(activity);
            }
            return activity;
        }

        private static void Record(string operation, string kind, long started, Exception? error)
        {
            var tags = new TagList
            {
                { OperationTag, operation },
                { KindTag, kind }
            };
            if (error != null)
            {
                tags.Add("error.type", error.GetType().FullName);
                _errors.Add(1, tags);
            }
            _requests.Add(1, tags);
            _duration.Record((Stopwatch.GetTimestamp() - started) / (double)Stopwatch.Frequency, tags);
        }

        /// <summary>
        /// Readable type name: <c>GetOrders</c>, <c>Paged&lt;Order&gt;</c>; generic arguments without assembly names.
        /// </summary>
        internal static string GetName(Type type) => _names.GetOrAdd(type, static t => Format(t));

        private static string Format(Type type)
        {
            if (!type.IsGenericType)
            {
                return type.Name;
            }
            var name = type.Name;
            var tick = name.IndexOf('`');
            return (tick > 0 ? name.Substring(0, tick) : name)
                + "<" + string.Join(",", type.GetGenericArguments().Select(Format)) + ">";
        }
    }
}
