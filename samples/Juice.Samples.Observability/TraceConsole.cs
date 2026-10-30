using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using OpenTelemetry;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;

namespace Juice.Samples.Observability
{
    /// <summary>
    /// Prints each trace as a tree once it is complete: every span with its parent, source and key tags, and the logs
    /// written inside each span under it. Metrics are printed as a summary whenever they change.
    /// <para>For learning only: a real host exports to a backend (OTLP) instead.</para>
    /// </summary>
    internal sealed class TraceConsole : IDisposable
    {
        // a trace is printed once no span or log has arrived for it for this long
        private static readonly TimeSpan Quiet = TimeSpan.FromMilliseconds(800);

        private static readonly string[] SpanTags =
        [
            "http.request.method", "url.path", "http.response.status_code",
            "messaging.system", "messaging.destination.name", "messaging.consumer.group.name",
            "juice.handler", "juice.dispatch.result", "juice.delivery.outcome", "error.type"
        ];

        private readonly ConcurrentDictionary<ActivityTraceId, TraceBuffer> _traces = new();
        private readonly Timer _timer;
        private readonly object _console = new();

        public TraceConsole()
        {
            Spans = new SpanProcessor(this);
            Logs = new LogProcessor(this);
            Metrics = new MetricsExporter(this);
            _timer = new Timer(_ => FlushQuietTraces(), null, TimeSpan.FromMilliseconds(250), TimeSpan.FromMilliseconds(250));
        }

        public BaseProcessor<Activity> Spans { get; }
        public BaseProcessor<LogRecord> Logs { get; }
        public BaseExporter<Metric> Metrics { get; }

        public void WriteLine(string text, ConsoleColor color = ConsoleColor.Gray)
        {
            lock (_console)
            {
                Console.ForegroundColor = color;
                Console.WriteLine(text);
                Console.ResetColor();
            }
        }

        public void Dispose()
        {
            _timer.Dispose();
            FlushQuietTraces(all: true);
        }

        private TraceBuffer Buffer(ActivityTraceId traceId) => _traces.GetOrAdd(traceId, _ => new TraceBuffer());

        private void FlushQuietTraces(bool all = false)
        {
            foreach (var (traceId, buffer) in _traces)
            {
                if ((all || DateTime.UtcNow - buffer.LastUpdate > Quiet) && _traces.TryRemove(traceId, out _))
                {
                    Print(traceId, buffer);
                }
            }
        }

        private void Print(ActivityTraceId traceId, TraceBuffer buffer)
        {
            List<SpanInfo> spans;
            List<LogInfo> logs;
            lock (buffer)
            {
                spans = buffer.Spans.ToList();
                logs = buffer.Logs.ToList();
            }
            var spanIds = spans.Select(s => s.SpanId).ToHashSet();
            var correlation = spans.Select(s => s.Tag("juice.correlation_id")).FirstOrDefault(c => c != null);

            var text = new StringBuilder();
            text.AppendLine();
            text.AppendLine($"══ trace {traceId}  correlation={correlation ?? "-"}  ({spans.Count} spans, {logs.Count} logs)");
            // roots: spans whose parent is not in this process (none, or a remote caller)
            var roots = spans.Where(s => !spanIds.Contains(s.ParentSpanId)).OrderBy(s => s.Start).ToList();
            for (var i = 0; i < roots.Count; i++)
            {
                var root = roots[i];
                if (root.ParentSpanId != default)
                {
                    text.AppendLine($"   (continues remote parent span {Short(root.ParentSpanId)} from the traceparent header)");
                }
                Render(text, root, spans, logs, "", i == roots.Count - 1);
            }
            foreach (var log in logs.Where(l => !spanIds.Contains(l.SpanId)))
            {
                text.AppendLine($"   · {Describe(log)}   (its span was not recorded)");
            }
            var hasError = spans.Any(s => s.Status == ActivityStatusCode.Error);
            WriteLine(text.ToString().TrimEnd(), hasError ? ConsoleColor.Yellow : ConsoleColor.Gray);
        }

        private static void Render(StringBuilder text, SpanInfo span, List<SpanInfo> spans, List<LogInfo> logs, string indent, bool last)
        {
            text.Append(indent).Append(last ? "└─ " : "├─ ").AppendLine(Describe(span));
            var childIndent = indent + (last ? "   " : "│  ");
            foreach (var exception in span.Exceptions)
            {
                text.Append(childIndent).AppendLine($"! exception {exception}");
            }
            // children and logs in time order
            var items = spans.Where(s => s.ParentSpanId == span.SpanId).Select(s => (s.Start, Span: (SpanInfo?)s, Log: (LogInfo?)null))
                .Concat(logs.Where(l => l.SpanId == span.SpanId).Select(l => (Start: l.Timestamp, Span: (SpanInfo?)null, Log: (LogInfo?)l)))
                .OrderBy(x => x.Start)
                .ToList();
            for (var i = 0; i < items.Count; i++)
            {
                var isLast = i == items.Count - 1;
                if (items[i].Span is { } child)
                {
                    Render(text, child, spans, logs, childIndent, isLast);
                }
                else
                {
                    text.Append(childIndent).Append(isLast ? "└· " : "├· ").AppendLine(Describe(items[i].Log!));
                }
            }
        }

        private static string Describe(SpanInfo span)
        {
            var source = span.Source switch
            {
                "Juice" => "Juice",
                "Microsoft.AspNetCore" => "ASP.NET Core",
                var other => other
            };
            var tags = string.Join(" ", SpanTags.Select(k => (k, v: span.Tag(k))).Where(t => !string.IsNullOrEmpty(t.v)).Select(t => $"{t.k}={t.v}"));
            var status = span.Status == ActivityStatusCode.Error ? $"  ERROR: {span.StatusDescription}" : "";
            return $"{span.Name}  [{source} · {span.Kind.ToString().ToLowerInvariant()}]  {span.Duration.TotalMilliseconds:0.0} ms  span={Short(span.SpanId)}  {tags}{status}".TrimEnd();
        }

        private static string Describe(LogInfo log)
        {
            var category = log.Category[(log.Category.LastIndexOf('.') + 1)..];
            var exception = log.Exception != null ? $"  exception={log.Exception}" : "";
            return $"log {log.Level} {category}: {log.Message}{exception}   ({log.Context})";
        }

        private static string Short(ActivitySpanId id) => id.ToHexString()[..8];

        private sealed class TraceBuffer
        {
            public List<SpanInfo> Spans { get; } = new();
            public List<LogInfo> Logs { get; } = new();
            public DateTime LastUpdate { get; set; } = DateTime.UtcNow;
        }

        private sealed record SpanInfo(ActivitySpanId SpanId, ActivitySpanId ParentSpanId, string Name, ActivityKind Kind,
            string Source, DateTime Start, TimeSpan Duration, ActivityStatusCode Status, string? StatusDescription,
            Dictionary<string, string?> Tags, List<string> Exceptions)
        {
            public string? Tag(string key) => Tags.TryGetValue(key, out var value) ? value : null;
        }

        private sealed record LogInfo(ActivitySpanId SpanId, DateTime Timestamp, string Level, string Category, string Message,
            string? Exception, string Context);

        private sealed class SpanProcessor(TraceConsole console) : BaseProcessor<Activity>
        {
            public override void OnEnd(Activity data)
            {
                var span = new SpanInfo(data.SpanId, data.ParentSpanId, data.DisplayName, data.Kind, data.Source.Name,
                    data.StartTimeUtc, data.Duration, data.Status, data.StatusDescription,
                    data.TagObjects.GroupBy(t => t.Key).ToDictionary(g => g.Key, g => g.Last().Value?.ToString()),
                    data.Events.Where(e => e.Name == "exception")
                        .Select(e => $"{e.Tags.FirstOrDefault(t => t.Key == "exception.type").Value}: {e.Tags.FirstOrDefault(t => t.Key == "exception.message").Value}")
                        .ToList());
                var buffer = console.Buffer(data.TraceId);
                lock (buffer)
                {
                    buffer.Spans.Add(span);
                    buffer.LastUpdate = DateTime.UtcNow;
                }
            }
        }

        private sealed class LogProcessor(TraceConsole console) : BaseProcessor<LogRecord>
        {
            public override void OnEnd(LogRecord data)
            {
                // the record is reused after this call: copy what is printed
                var attributes = data.Attributes ?? [];
                string? Attribute(string key) => attributes.FirstOrDefault(a => a.Key == key).Value?.ToString();
                var context = $"juice.correlation_id={Attribute("juice.correlation_id") ?? "-"} juice.causation_id={Attribute("juice.causation_id") ?? "-"}";
                var log = new LogInfo(data.SpanId, data.Timestamp, data.LogLevel.ToString(), data.CategoryName ?? "",
                    data.FormattedMessage ?? data.Body ?? "", data.Exception?.Message, context);
                if (data.TraceId == default)
                {
                    console.WriteLine($"   (no trace) {Describe(log)}", ConsoleColor.DarkGray);
                    return;
                }
                var buffer = console.Buffer(data.TraceId);
                lock (buffer)
                {
                    buffer.Logs.Add(log);
                    buffer.LastUpdate = DateTime.UtcNow;
                }
            }
        }

        /// <summary>
        /// Prints the Juice metrics when they change (cumulative values since start).
        /// </summary>
        private sealed class MetricsExporter(TraceConsole console) : BaseExporter<Metric>
        {
            private string _last = "";

            public override ExportResult Export(in Batch<Metric> batch)
            {
                var lines = new List<string>();
                foreach (var metric in batch)
                {
                    foreach (ref readonly var point in metric.GetMetricPoints())
                    {
                        var tags = new List<string>();
                        foreach (var tag in point.Tags)
                        {
                            tags.Add($"{tag.Key}={tag.Value}");
                        }
                        var value = metric.MetricType switch
                        {
                            MetricType.LongSum or MetricType.LongSumNonMonotonic => point.GetSumLong().ToString(),
                            MetricType.DoubleSum or MetricType.DoubleSumNonMonotonic => point.GetSumDouble().ToString("0.###"),
                            MetricType.LongGauge => point.GetGaugeLastValueLong().ToString(),
                            MetricType.DoubleGauge => point.GetGaugeLastValueDouble().ToString("0.###"),
                            MetricType.Histogram => $"count={point.GetHistogramCount()} sum={point.GetHistogramSum():0.####}{metric.Unit}",
                            _ => metric.MetricType.ToString()
                        };
                        lines.Add($"   {metric.MeterName,-16} {metric.Name} {{{string.Join(", ", tags)}}} = {value}");
                    }
                }
                lines.Sort(StringComparer.Ordinal);
                var text = string.Join(Environment.NewLine, lines);
                if (lines.Count > 0 && text != _last)
                {
                    _last = text;
                    console.WriteLine($"{Environment.NewLine}══ metrics (cumulative since start){Environment.NewLine}{text}", ConsoleColor.DarkCyan);
                }
                return ExportResult.Success;
            }
        }
    }
}
