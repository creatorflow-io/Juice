using System.Diagnostics;
using System.Text.Json;
using FluentAssertions;
using Juice.MediatR;
using Juice.Messaging;
using Juice.Messaging.Outbox;
using Juice.Messaging.Outbox.Delivery.Internal;
using Juice.Messaging.Outbox.EF;
using Juice.Messaging.Policies;
using Juice.OpenTelemetry.TestConsumer;
using Juice.XUnit;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Trace;

namespace Juice.OpenTelemetry.Tests
{
    /// <summary>
    /// One trace across two OS processes connected only by RabbitMQ: this test process runs the "orders" service
    /// (HTTP, mediator, outbox, delivery, producer) and starts <c>Juice.OpenTelemetry.TestConsumer</c> as the
    /// "billing" service. The consumer can only continue the trace through the <c>traceparent</c> message header.
    /// <para>Needs the shared RabbitMQ and SQL Server (<c>test/infra/setup-test-infra.ps1</c>).</para>
    /// </summary>
    public class CrossProcessTraceTests
    {
        private const string Exchange = "x.juice.otel.xunit";
        private const string Queue = "juice_otel_xunit";

        private readonly ITestOutputHelper _output;

        public CrossProcessTraceTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [IgnoreOnCIFact]
        public async Task Trace_should_continue_in_the_consumer_process_Async()
        {
            var configuration = new ConfigurationBuilder()
                .AddUserSecrets(typeof(CrossProcessTraceTests).Assembly, optional: true)
                .AddEnvironmentVariables()
                .Build();
            var rabbitMQ = configuration.GetSection("Juice:EventBus:Connections:RabbitMQ");
            var sqlServer = configuration.GetConnectionString("SqlServerConnection")
                ?? throw new InvalidOperationException("ConnectionStrings:SqlServerConnection is not set: run test/infra/setup-test-infra.ps1.");
            var connectionString = new SqlConnectionStringBuilder(sqlServer)
            {
                InitialCatalog = $"JuiceOtelTests_{Guid.NewGuid():N}",
                TrustServerCertificate = true
            }.ConnectionString;

            await using var consumer = await ConsumerProcess.StartAsync(rabbitMQ, Exchange, Queue, _output);
            await using var orders = await OrdersService.StartAsync(connectionString, rabbitMQ, _output);

            // the caller of the orders service, as an upstream service would send it
            var traceId = ActivityTraceId.CreateRandom();
            var callerSpanId = ActivitySpanId.CreateRandom();
            using var request = new HttpRequestMessage(HttpMethod.Post, "/orders");
            request.Headers.Add("traceparent", $"00-{traceId}-{callerSpanId}-01");
            request.Headers.Add("x-correlation-id", "corr-xproc");
            using (var response = await orders.Host.GetTestClient().SendAsync(request))
            {
                response.EnsureSuccessStatusCode();
            }

            // billing (the other process)
            var handle = await consumer.WaitForSpanAsync(traceId, "handle " + nameof(TracedOrderPlacedHandler));
            var billing = consumer.ReadSpans().Where(s => s.TraceId == traceId.ToHexString()).ToList();
            // orders (this process): the delivery span stops after the outbox row is marked published
            await WaitForAsync(orders.Spans, traceId, "deliver rabbitmq");
            var trace = orders.Spans.Snapshot().Where(a => a.TraceId == traceId).ToList();

            _output.WriteLine("orders (pid {0}):", Environment.ProcessId);
            foreach (var a in trace) _output.WriteLine($"  {a.Kind,-9} {a.OperationName} {a.SpanId} <- {a.ParentSpanId}");
            _output.WriteLine("billing (pid {0}):", consumer.ProcessId);
            foreach (var s in billing) _output.WriteLine($"  {s.Kind,-9} {s.Name} {s.SpanId} <- {s.ParentSpanId}");

            var server = trace.Should().ContainSingle(a => a.Kind == ActivityKind.Server).Subject;
            server.ParentSpanId.Should().Be(callerSpanId);
            var mediator = trace.Should().ContainSingle(a => a.OperationName == "mediator " + nameof(SubmitOrder)).Subject;
            mediator.ParentSpanId.Should().Be(server.SpanId);
            var create = trace.Should().ContainSingle(a => a.OperationName == "create " + nameof(TracedOrderPlaced)).Subject;
            create.ParentSpanId.Should().Be(mediator.SpanId);
            var deliver = trace.Should().ContainSingle(a => a.OperationName == "deliver rabbitmq").Subject;
            deliver.ParentSpanId.Should().Be(create.SpanId, "the background delivery continues the context stored in the outbox row");
            deliver.GetTagItem("juice.delivery.outcome").Should().Be("published");
            var send = trace.Should().ContainSingle(a => a.OperationName == "send " + Exchange).Subject;
            send.Kind.Should().Be(ActivityKind.Producer);
            send.ParentSpanId.Should().Be(deliver.SpanId);
            send.GetTagItem("messaging.system").Should().Be("rabbitmq");

            var process = billing.Should().ContainSingle(s => s.Name == "process " + Queue).Subject;
            process.ProcessId.Should().Be(consumer.ProcessId).And.NotBe(Environment.ProcessId);
            process.Source.Should().Be("Juice");
            process.Kind.Should().Be(nameof(ActivityKind.Consumer));
            process.ParentSpanId.Should().Be(create.SpanId.ToHexString(), "the message carries the context it was created in");
            process.Tags["messaging.system"].Should().Be("rabbitmq");
            process.Tags["messaging.consumer.group.name"].Should().Be(Queue);
            process.Tags["juice.dispatch.result"].Should().Be("success");
            process.Tags["juice.correlation_id"].Should().Be("corr-xproc", "the correlation id travels in its own header");
            process.Tags["juice.causation_id"].Should().Be((string?)create.GetTagItem("messaging.message.id"));
            process.Tags["juice.source"].Should().Be("billing");
            handle.ParentSpanId.Should().Be(process.SpanId);
        }

        private static async Task<Activity> WaitForAsync(LockedList<Activity> spans, ActivityTraceId traceId, string name)
        {
            Activity? Find() => spans.Snapshot().FirstOrDefault(a => a.TraceId == traceId && a.OperationName == name);
            await Waiter.WaitAsync(() => Find() != null, TimeSpan.FromSeconds(15));
            return Find() ?? throw new TimeoutException($"No span '{name}'. Exported: "
                + string.Join(", ", spans.Snapshot().Where(a => a.TraceId == traceId).Select(a => a.OperationName)));
        }

        /// <summary>
        /// The "orders" service in this process: HTTP, mediator, outbox on SQL Server, delivery to RabbitMQ.
        /// </summary>
        private sealed class OrdersService : IAsyncDisposable
        {
            private readonly string _connectionString;
            public IHost Host { get; }
            public LockedList<Activity> Spans { get; }

            private OrdersService(IHost host, LockedList<Activity> spans, string connectionString)
            {
                Host = host;
                Spans = spans;
                _connectionString = connectionString;
            }

            public static async Task<OrdersService> StartAsync(string connectionString, IConfigurationSection rabbitMQ, ITestOutputHelper output)
            {
                await using (var context = new OrdersOutboxContext(new DbContextOptionsBuilder<OrdersOutboxContext>().UseSqlServer(connectionString).Options))
                {
                    await context.Database.EnsureCreatedAsync();
                }
                var spans = new LockedList<Activity>();
                var host = await new HostBuilder()
                    .ConfigureWebHost(web => web
                        .UseTestServer()
                        .ConfigureServices(services =>
                        {
                            services.AddLogging(b => b.AddTestOutputLogger(output).SetMinimumLevel(LogLevel.Information));
                            services.AddRouting();
                            services.AddOpenTelemetry().WithTracing(tracing => tracing
                                .AddAspNetCoreInstrumentation()
                                .AddJuiceInstrumentation()
                                .AddInMemoryExporter(spans));
                            services.AddMediatR(b => b.RegisterServicesFromAssemblyContaining<CrossProcessTraceTests>(includeNonPublicTypes: false));
                            services.AddJuiceOpenTelemetry();

                            services.AddDbContext<OrdersOutboxContext>(o => o.UseSqlServer(connectionString));
                            services.AddSingleton<IMessagePublishingPolicy>(new FixedRoutePolicy("rabbitmq", Exchange));
                            services.AddMessaging()
                                .AddOutbox()
                                .AddMessageService<OrdersOutboxContext>()
                                .AddIdempotencyInMemory()
                                .AddDelivery(delivery =>
                                {
                                    delivery.AddDeliveryPolicies(o => o.DefaultPolicy = new PolicyConfiguration
                                    {
                                        Interval = TimeSpan.FromMilliseconds(100),
                                        BatchSize = 10,
                                        Timeout = TimeSpan.FromSeconds(10),
                                        InitialRetryDelay = TimeSpan.FromMilliseconds(200),
                                        RetryDelayMultiplier = 1.0,
                                        MaxRetryAttempts = 5
                                    });
                                    delivery.AddDeliveryProcessor<OrdersOutboxContext>("rabbitmq");
                                    delivery.EventBus.AddRabbitMQ(r => r
                                        .AddConnection("rabbitmq", rabbitMQ)
                                        .AddProducer("rabbitmq", "rabbitmq"));
                                });
                        })
                        .Configure(app =>
                        {
                            app.UseMessageContext("orders");
                            app.UseRouting();
                            app.UseEndpoints(endpoints => endpoints.MapPost("/orders", async (IMediator mediator) =>
                            {
                                await mediator.Send(new SubmitOrder("A-1"));
                                return Results.Accepted();
                            }));
                        }))
                    .StartAsync();
                return new OrdersService(host, spans, connectionString);
            }

            public async ValueTask DisposeAsync()
            {
                using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10)))
                {
                    await Host.StopAsync(cts.Token);
                }
                Host.Dispose();
                await using var context = new OrdersOutboxContext(new DbContextOptionsBuilder<OrdersOutboxContext>().UseSqlServer(_connectionString).Options);
                await context.Database.EnsureDeletedAsync();
            }
        }

        /// <summary>
        /// The "billing" service: <c>Juice.OpenTelemetry.TestConsumer</c> in its own OS process, exporting its spans
        /// as JSON lines.
        /// </summary>
        private sealed class ConsumerProcess : IAsyncDisposable
        {
            private readonly Process _process;
            private readonly string _spansFile;

            public int ProcessId => _process.Id;

            private ConsumerProcess(Process process, string spansFile)
            {
                _process = process;
                _spansFile = spansFile;
            }

            public static async Task<ConsumerProcess> StartAsync(IConfigurationSection rabbitMQ, string exchange, string queue, ITestOutputHelper output)
            {
                // built next to this project: build/bin/<Configuration>/<Project>/<tfm>/
                var baseDirectory = AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                var tfm = Path.GetFileName(baseDirectory);
                var dll = Path.GetFullPath(Path.Combine(baseDirectory, "..", "..", "Juice.OpenTelemetry.TestConsumer", tfm, "Juice.OpenTelemetry.TestConsumer.dll"));
                if (!File.Exists(dll))
                {
                    throw new FileNotFoundException("Build the solution first: the consumer service was not found.", dll);
                }
                var spansFile = Path.Combine(Path.GetTempPath(), $"juice-otel-{Guid.NewGuid():N}.jsonl");

                var start = new ProcessStartInfo("dotnet")
                {
                    RedirectStandardInput = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false
                };
                start.ArgumentList.Add(dll);
                foreach (var setting in rabbitMQ.AsEnumerable(makePathsRelative: true).Where(s => s.Value != null))
                {
                    start.Environment["Juice__EventBus__Connections__RabbitMQ__" + setting.Key.Replace(":", "__")] = setting.Value;
                }
                start.Environment["JUICE_OTEL_SPANS_FILE"] = spansFile;
                start.Environment["JUICE_OTEL_EXCHANGE"] = exchange;
                start.Environment["JUICE_OTEL_QUEUE"] = queue;
                start.Environment["Logging__LogLevel__Default"] = "Warning";

                var process = Process.Start(start) ?? throw new InvalidOperationException("Could not start the consumer service.");
                var consumer = new ConsumerProcess(process, spansFile);
                var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                var errors = new List<string>();
                process.OutputDataReceived += (_, e) =>
                {
                    if (e.Data == "READY") ready.TrySetResult();
                };
                process.ErrorDataReceived += (_, e) =>
                {
                    if (e.Data != null) lock (errors) errors.Add(e.Data);
                };
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                var exited = process.WaitForExitAsync();
                if (await Task.WhenAny(ready.Task, exited, Task.Delay(TimeSpan.FromSeconds(30))) != ready.Task)
                {
                    var stderr = string.Join(Environment.NewLine, errors);
                    await consumer.DisposeAsync();
                    throw new InvalidOperationException($"The consumer service did not start. {stderr}");
                }
                output.WriteLine($"billing consumer started, pid {process.Id}");
                return consumer;
            }

            public List<ExportedSpan> ReadSpans()
            {
                if (!File.Exists(_spansFile))
                {
                    return [];
                }
                using var stream = new FileStream(_spansFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                using var reader = new StreamReader(stream);
                var spans = new List<ExportedSpan>();
                while (reader.ReadLine() is { } line)
                {
                    try
                    {
                        spans.Add(JsonSerializer.Deserialize<ExportedSpan>(line)!);
                    }
                    catch (JsonException)
                    {
                        // the line being written
                    }
                }
                return spans;
            }

            public async Task<ExportedSpan> WaitForSpanAsync(ActivityTraceId traceId, string name)
            {
                ExportedSpan? Find() => ReadSpans().FirstOrDefault(s => s.TraceId == traceId.ToHexString() && s.Name == name);
                await Waiter.WaitAsync(() => Find() != null, TimeSpan.FromSeconds(30));
                return Find() ?? throw new TimeoutException($"The consumer service exported no span '{name}' in trace {traceId}.");
            }

            public async ValueTask DisposeAsync()
            {
                if (!_process.HasExited)
                {
                    // closing standard input stops the service
                    _process.StandardInput.Close();
                    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
                    try
                    {
                        await _process.WaitForExitAsync(cts.Token);
                    }
                    catch (OperationCanceledException)
                    {
                        _process.Kill(entireProcessTree: true);
                    }
                }
                _process.Dispose();
                try { File.Delete(_spansFile); } catch (IOException) { }
            }
        }
    }

    public class OrdersOutboxContext : DbContext, IOutboxContext
    {
        public OrdersOutboxContext(DbContextOptions<OrdersOutboxContext> options) : base(options)
        {
        }

        public DbSet<OutboxEvent> Outbox { get; set; } = default!;
        public DbSet<OutboxDelivery> OutboxDeliveries { get; set; } = default!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            this.ConfigureOutbox(modelBuilder);
        }
    }

    public sealed record SubmitOrder(string OrderNumber) : MessageBase, IRequest;

    public sealed class SubmitOrderHandler(IMessageService<OrdersOutboxContext> messages) : IRequestHandler<SubmitOrder>
    {
        public async ValueTask Handle(SubmitOrder request, CancellationToken cancellationToken)
            => await messages.PublishAsync(new TracedOrderPlaced { OrderNumber = request.OrderNumber }, cancellationToken);
    }
}
