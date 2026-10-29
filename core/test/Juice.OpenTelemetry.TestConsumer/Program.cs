using System.Diagnostics;
using System.Text.Json;
using Juice.Messaging;
using Juice.OpenTelemetry.TestConsumer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenTelemetry;
using OpenTelemetry.Trace;
using RabbitMQ.Client;

// A separate service consuming from RabbitMQ: it shares nothing with the test process but the broker.
// Settings come from environment variables (Juice__EventBus__Connections__RabbitMQ__*, JUICE_OTEL_*).
// Prints READY once consuming; stops when its standard input closes.
var builder = Host.CreateApplicationBuilder(args);
var spansFile = builder.Configuration["JUICE_OTEL_SPANS_FILE"] ?? throw new InvalidOperationException("JUICE_OTEL_SPANS_FILE is required.");
var exchange = builder.Configuration["JUICE_OTEL_EXCHANGE"] ?? throw new InvalidOperationException("JUICE_OTEL_EXCHANGE is required.");
var queue = builder.Configuration["JUICE_OTEL_QUEUE"] ?? throw new InvalidOperationException("JUICE_OTEL_QUEUE is required.");

builder.Services.AddOpenTelemetry()
    .WithTracing(tracing => tracing
        .AddJuiceInstrumentation()
        .AddProcessor(new SimpleActivityExportProcessor(new JsonLinesExporter(spansFile))));

builder.Services.AddMessaging().AddIdempotencyInMemory();
builder.Services.AddEventBus().AddRabbitMQ(rabbitMQ => rabbitMQ
    .AddConnection("rabbitmq", builder.Configuration.GetSection("Juice:EventBus:Connections:RabbitMQ"))
    .AddInfrastructureTopology("rabbitmq", topology => topology
        .DeclareExchange(exchange, ExchangeType.Topic)
        .DeclareQueue(queue)
        .BindQueue(queue, exchange, nameof(TracedOrderPlaced)))
    .AddConsumer("billing", queue, "rabbitmq", consumer => consumer
        .Subscribe<TracedOrderPlaced, TracedOrderPlacedHandler>()));

using var host = builder.Build();
await host.InitRabbitMQInfrastructureAsync();
await host.StartAsync();
Console.WriteLine("READY");
await Task.Run(Console.In.ReadToEnd);
await host.StopAsync();

namespace Juice.OpenTelemetry.TestConsumer
{
    /// <summary>
    /// The integration event of the cross-process test, shared by the producer (the test) and this consumer.
    /// </summary>
    public sealed record TracedOrderPlaced : IntegrationEvent
    {
        public string? OrderNumber { get; init; }
    }

    public sealed class TracedOrderPlacedHandler : IIntegrationEventHandler<TracedOrderPlaced>
    {
        public Task HandleAsync(TracedOrderPlaced @event) => Task.CompletedTask;
    }

    /// <summary>
    /// One JSON object per finished span, so the test process can read this service's spans.
    /// </summary>
    internal sealed class JsonLinesExporter(string path) : BaseExporter<Activity>
    {
        private readonly object _lock = new();

        public override ExportResult Export(in Batch<Activity> batch)
        {
            lock (_lock)
            {
                using var writer = File.AppendText(path);
                foreach (var activity in batch)
                {
                    writer.WriteLine(JsonSerializer.Serialize(new ExportedSpan
                    {
                        ProcessId = Environment.ProcessId,
                        Source = activity.Source.Name,
                        Name = activity.OperationName,
                        Kind = activity.Kind.ToString(),
                        TraceId = activity.TraceId.ToHexString(),
                        SpanId = activity.SpanId.ToHexString(),
                        ParentSpanId = activity.ParentSpanId.ToHexString(),
                        Status = activity.Status.ToString(),
                        Tags = activity.TagObjects.ToDictionary(t => t.Key, t => t.Value?.ToString())
                    }));
                }
            }
            return ExportResult.Success;
        }
    }

    public sealed class ExportedSpan
    {
        public int ProcessId { get; init; }
        public string Source { get; init; } = default!;
        public string Name { get; init; } = default!;
        public string Kind { get; init; } = default!;
        public string TraceId { get; init; } = default!;
        public string SpanId { get; init; } = default!;
        public string ParentSpanId { get; init; } = default!;
        public string Status { get; init; } = default!;
        public Dictionary<string, string?> Tags { get; init; } = new();
    }
}
