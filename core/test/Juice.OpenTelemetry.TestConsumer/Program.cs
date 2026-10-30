using System.Diagnostics;
using System.Text.Json;
using Juice.EventBus.NATS;
using Juice.EventBus.NATS.Infrastructure;
using Juice.MediatR;
using Juice.Messaging;
using Juice.OpenTelemetry.TestConsumer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenTelemetry;
using OpenTelemetry.Trace;
using RabbitMQ.Client;

// A separate "billing" service consuming from RabbitMQ or NATS JetStream: it shares nothing with the test process but
// the broker. Each event goes through the mediator to a database write, as a real consumer would.
// Settings come from environment variables (Juice__EventBus__Connections__RabbitMQ__*, Juice__EventBus__Connections__Nats__*,
// JUICE_OTEL_*). Prints READY once consuming; stops when its standard input closes.
var builder = Host.CreateApplicationBuilder(args);
string Required(string key) => builder.Configuration[key] ?? throw new InvalidOperationException($"{key} is required.");
var spansFile = Required("JUICE_OTEL_SPANS_FILE");
var transport = Required("JUICE_OTEL_TRANSPORT");
// RabbitMQ exchange / NATS destination
var destination = Required("JUICE_OTEL_DESTINATION");
// RabbitMQ queue / NATS durable consumer
var queue = Required("JUICE_OTEL_QUEUE");
var billingDatabase = Required("JUICE_OTEL_BILLING_DB");

builder.Services.AddOpenTelemetry()
    .WithTracing(tracing => tracing
        .AddJuiceInstrumentation()
        .AddSqlClientInstrumentation()
        .AddProcessor(new SimpleActivityExportProcessor(new JsonLinesExporter(spansFile))));
builder.Services.AddJuiceOpenTelemetry();
builder.Services.AddMediatR(m => m.RegisterServicesFromAssemblyContaining<RecordInvoice>(includeNonPublicTypes: false));
builder.Services.AddDbContext<BillingContext>(o => o.UseSqlServer(billingDatabase));

builder.Services.AddMessaging().AddIdempotencyInMemory();
switch (transport)
{
    case "rabbitmq":
        builder.Services.AddEventBus().AddRabbitMQ(rabbitMQ => rabbitMQ
            .AddConnection("rabbitmq", builder.Configuration.GetSection("Juice:EventBus:Connections:RabbitMQ"))
            .AddInfrastructureTopology("rabbitmq", topology => topology
                .DeclareExchange(destination, ExchangeType.Topic)
                .DeclareQueue(queue)
                .BindQueue(queue, destination, nameof(TracedOrderPlaced)))
            .AddConsumer("billing", queue, "rabbitmq", consumer => consumer
                .Subscribe<TracedOrderPlaced, TracedOrderPlacedHandler>()));
        break;
    case "nats":
        builder.Services.AddEventBus().AddJetStream(js => js
            .AddConnection("nats", builder.Configuration.GetSection("Juice:EventBus:Connections:Nats"))
            .AddInfrastructureTopology("nats", topology => topology
                .Provisioning(JetStreamProvisioning.CreateIfMissing)
                .DeclareStream(destination, stream => stream.Name = Stream(destination)))
            .AddConsumer("billing", Stream(destination), queue, "nats", consumer => consumer
                .Destination(destination)
                .Provisioning(JetStreamProvisioning.CreateIfMissing)
                .Subscribe<TracedOrderPlaced, TracedOrderPlacedHandler>()));
        break;
    default:
        throw new InvalidOperationException($"Unknown JUICE_OTEL_TRANSPORT '{transport}'.");
}

using var host = builder.Build();
await using (var scope = host.Services.CreateAsyncScope())
{
    await scope.ServiceProvider.GetRequiredService<BillingContext>().Database.EnsureCreatedAsync();
}
if (transport == "rabbitmq")
{
    await host.InitRabbitMQInfrastructureAsync();
}
else
{
    // the stream exists before the producer publishes
    await host.InitJetStreamInfrastructureAsync();
}
await host.StartAsync();
Console.WriteLine("READY");
await Task.Run(Console.In.ReadToEnd);
await host.StopAsync();

// named here: the consumer must name the stream the topology declares
static string Stream(string destination) => destination.ToUpperInvariant().Replace('.', '_');

namespace Juice.OpenTelemetry.TestConsumer
{
    /// <summary>
    /// The integration event of the cross-process test, shared by the producer (the test) and this consumer.
    /// </summary>
    public sealed record TracedOrderPlaced : IntegrationEvent
    {
        public string? OrderNumber { get; init; }
    }

    public sealed class TracedOrderPlacedHandler(IMediator mediator) : IIntegrationEventHandler<TracedOrderPlaced>
    {
        public async Task HandleAsync(TracedOrderPlaced @event)
            => await mediator.Send(new RecordInvoice(@event.OrderNumber ?? ""));
    }

    public sealed record RecordInvoice(string OrderNumber) : MessageBase, IRequest;

    public sealed class RecordInvoiceHandler(BillingContext billing) : IRequestHandler<RecordInvoice>
    {
        public async ValueTask Handle(RecordInvoice request, CancellationToken cancellationToken)
        {
            billing.Invoices.Add(new Invoice { OrderNumber = request.OrderNumber });
            await billing.SaveChangesAsync(cancellationToken);
        }
    }

    /// <summary>
    /// The billing service's own database; the test creates a fresh one per run and deletes it afterwards.
    /// </summary>
    public class BillingContext(DbContextOptions<BillingContext> options) : DbContext(options)
    {
        public DbSet<Invoice> Invoices { get; set; } = default!;
    }

    public class Invoice
    {
        public int Id { get; set; }
        public string OrderNumber { get; set; } = "";
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
