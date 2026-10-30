using System.Diagnostics;
using System.Text;
using FluentAssertions;
using Juice.Messaging;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Exporter;
using OpenTelemetry.Logs;
using OpenTelemetry.Trace;

namespace Juice.OpenTelemetry.Tests
{
    /// <summary>
    /// Logs through the host's OpenTelemetry logging with the Juice log processor: trace correlation, the Juice
    /// context, payload redaction, and exporter outages.
    /// </summary>
    public class LogExportTests
    {
        // a Juice category: payload redaction applies to Juice logs only
        private const string JuiceCategory = "Juice.MediatR.Behaviors.TransactionBehavior";
        private const string Secret = "4111-1111-1111-1111";

        private readonly ITestOutputHelper _output;

        public LogExportTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public void Log_in_an_active_span_should_carry_trace_span_and_juice_context()
        {
            var records = new LockedList<LogRecord>();
            using var services = Logging(l => l.AddJuiceInstrumentation().AddInMemoryExporter(records));
            using var tracing = Sdk.CreateTracerProviderBuilder().AddSource(TestSources.Root.Name).Build();
            var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger(JuiceCategory);

            MessageContext.Initialize("corr-log", "cause-log", "exec-log", "orders-api");
            try
            {
                using var span = TestSources.Root.StartActivity("log test");
                logger.LogInformation("Order {OrderNumber} placed", 42);

                Flush(services);
                var record = records.Should().ContainSingle().Subject;
                record.TraceId.Should().Be(span!.TraceId);
                record.SpanId.Should().Be(span.SpanId);
                var attributes = Attributes(record);
                attributes.Should().Contain("OrderNumber", 42);
                attributes.Should().Contain("juice.correlation_id", "corr-log");
                attributes.Should().Contain("juice.causation_id", "cause-log");
                attributes.Should().Contain("juice.execution_id", "exec-log");
                attributes.Should().Contain("juice.source", "orders-api");
            }
            finally
            {
                MessageContext.Clear();
            }
        }

        [Fact]
        public void Juice_logs_should_not_export_payloads_by_default()
        {
            var records = new LockedList<LogRecord>();
            using var services = Logging(l => l.AddJuiceInstrumentation().AddInMemoryExporter(records), formattedMessage: true);
            var factory = services.GetRequiredService<ILoggerFactory>();
            var logger = factory.CreateLogger(JuiceCategory);

            // the shapes of TransactionBehavior's error log and DeliveryProcessor's debug log
            logger.LogError(new InvalidOperationException("boom"), "ERROR Handling transaction for {CommandName} ({@Command})",
                nameof(PayCommand), new PayCommand(Secret));
            logger.LogInformation("Publishing outbox event. Payload: {Payload}", $"{{\"card\":\"{Secret}\"}}");
            // the application decides what its own logs contain
            factory.CreateLogger("Orders.Api").LogInformation("Paying {@Command}", new PayCommand(Secret));

            Flush(services);
            records.Should().HaveCount(3);
            foreach (var record in records.Where(r => r.CategoryName == JuiceCategory))
            {
                Exported(record).Should().NotContain(Secret, "a payload can hold personal data or secrets");
            }
            Attributes(records.First()).Should().Contain("CommandName", nameof(PayCommand), "scalar values stay");
            Exported(records.Single(r => r.CategoryName == "Orders.Api")).Should().Contain(Secret);
        }

        [Fact]
        public void IncludePayloads_should_export_Juice_payloads()
        {
            var records = new LockedList<LogRecord>();
            using var services = Logging(l => l.AddJuiceInstrumentation(o => o.IncludePayloads = true).AddInMemoryExporter(records));
            var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger(JuiceCategory);

            logger.LogInformation("Publishing outbox event. Payload: {Payload}", Secret);

            Flush(services);
            Attributes(records.Should().ContainSingle().Subject).Should().Contain("Payload", Secret);
        }

        [Fact]
        public async Task Otlp_export_should_deliver_the_log_with_its_trace_context_Async()
        {
            var requests = new LockedList<byte[]>();
            await using var receiver = await StartOtlpReceiverAsync(requests);
            var endpoint = new Uri(receiver.Urls.First() + "/v1/logs");
            using var services = Logging(l => l.AddJuiceInstrumentation().AddOtlpExporter(o =>
            {
                o.Endpoint = endpoint;
                o.Protocol = OtlpExportProtocol.HttpProtobuf;
            }));
            using var tracing = Sdk.CreateTracerProviderBuilder().AddSource(TestSources.Root.Name).Build();
            var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger(JuiceCategory);

            ActivityTraceId traceId;
            MessageContext.Initialize("corr-otlp", null, "exec-otlp", "orders-api");
            try
            {
                using var span = TestSources.Root.StartActivity("otlp log test");
                traceId = span!.TraceId;
                logger.LogWarning("Delivery {DeliveryOutcome} for {Command}", "parked", nameof(PayCommand));
                logger.LogInformation("Publishing outbox event. Payload: {Payload}", Secret);
            }
            finally
            {
                MessageContext.Clear();
            }
            Flush(services);

            // ExportLogsServiceRequest protobuf: look for the raw values rather than decode it
            var body = requests.Should().ContainSingle("one batch reaches the receiver").Subject;
            _output.WriteLine($"{body.Length} bytes to {endpoint}");
            var traceIdBytes = new byte[16];
            traceId.CopyTo(traceIdBytes);
            body.AsSpan().IndexOf(traceIdBytes).Should().BeGreaterThanOrEqualTo(0, "the log carries the trace id");
            Contains(body, "Delivery {DeliveryOutcome} for {Command}").Should().BeTrue();
            Contains(body, "juice.correlation_id").Should().BeTrue();
            Contains(body, "corr-otlp").Should().BeTrue();
            Contains(body, Secret).Should().BeFalse("payloads are not exported by default");
        }

        [Fact]
        public void Unreachable_otlp_endpoint_should_not_fail_logging()
        {
            using var services = Logging(l => l.AddJuiceInstrumentation().AddOtlpExporter(o =>
            {
                // nothing listens on the discard port
                o.Endpoint = new Uri("http://127.0.0.1:9/v1/logs");
                o.Protocol = OtlpExportProtocol.HttpProtobuf;
                o.TimeoutMilliseconds = 1000;
            }));
            var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger(JuiceCategory);

            var logging = () =>
            {
                for (var i = 0; i < 1000; i++)
                {
                    logger.LogInformation("Order {OrderNumber} placed", i);
                }
                Flush(services);
            };

            logging.Should().NotThrow();
        }

        [Fact]
        public void Stalled_exporter_should_neither_block_logging_nor_grow_the_queue()
        {
            const int maxQueueSize = 100;
            const int maxBatchSize = 10;
            var exporter = new StalledExporter();
            using var services = Logging(l => l.AddJuiceInstrumentation().AddProcessor(new BatchLogRecordExportProcessor(
                exporter, maxQueueSize, scheduledDelayMilliseconds: 10, exporterTimeoutMilliseconds: 30000, maxBatchSize)));
            var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger(JuiceCategory);

            var sw = Stopwatch.StartNew();
            for (var i = 0; i < 10_000; i++)
            {
                logger.LogInformation("Order {OrderNumber} placed", i);
            }
            sw.Stop();
            _output.WriteLine($"10000 logs in {sw.ElapsedMilliseconds} ms while the exporter is stalled");
            sw.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(5), "logging does not wait for the exporter");

            exporter.Release.Set();
            Flush(services);
            exporter.Exported.Should().BePositive()
                .And.BeLessThanOrEqualTo(maxQueueSize + maxBatchSize, "records beyond the queue are dropped, not buffered");
        }

        private static ServiceProvider Logging(Action<LoggerProviderBuilder> configure, bool formattedMessage = false)
        {
            var services = new ServiceCollection();
            services.AddOpenTelemetry().WithLogging(configure, o => o.IncludeFormattedMessage = formattedMessage);
            return services.BuildServiceProvider();
        }

        private static void Flush(ServiceProvider services)
            => services.GetRequiredService<LoggerProvider>().ForceFlush();

        private static Dictionary<string, object?> Attributes(LogRecord record)
            => (record.Attributes ?? []).ToDictionary(a => a.Key, a => a.Value);

        /// <summary>
        /// Everything an exporter could send for the record.
        /// </summary>
        private static string Exported(LogRecord record)
            => string.Join(" | ", new[] { record.FormattedMessage, record.Body, record.Exception?.ToString() }
                .Concat((record.Attributes ?? []).Select(a => $"{a.Key}={a.Value}")));

        private static bool Contains(byte[] body, string value)
            => body.AsSpan().IndexOf(Encoding.UTF8.GetBytes(value)) >= 0;

        private static async Task<WebApplication> StartOtlpReceiverAsync(ICollection<byte[]> requests)
        {
            var builder = WebApplication.CreateSlimBuilder();
            builder.WebHost.UseUrls("http://127.0.0.1:0");
            var app = builder.Build();
            app.MapPost("/v1/logs", async (HttpRequest request) =>
            {
                using var body = new MemoryStream();
                await request.Body.CopyToAsync(body);
                requests.Add(body.ToArray());
                return Results.Ok();
            });
            await app.StartAsync();
            return app;
        }

        private sealed record PayCommand(string CardNumber);

        /// <summary>
        /// An exporter whose backend hangs until released.
        /// </summary>
        private sealed class StalledExporter : BaseExporter<LogRecord>
        {
            private int _exported;

            public ManualResetEventSlim Release { get; } = new();
            public int Exported => Volatile.Read(ref _exported);

            public override ExportResult Export(in Batch<LogRecord> batch)
            {
                Release.Wait(TimeSpan.FromSeconds(30));
                Interlocked.Add(ref _exported, (int)batch.Count);
                return ExportResult.Failure;
            }
        }
    }
}
