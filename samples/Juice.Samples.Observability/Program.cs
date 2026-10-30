using Juice.MediatR;
using Juice.Messaging;
using Juice.Messaging.Policies;
using Juice.Samples.Observability;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

// One process plays both services: "orders" (HTTP → mediator → publish OrderPlaced) and "billing" (consume
// OrderPlaced → mediator → CreateInvoice). The event travels over the in-process local-channel transport, so the app
// needs no broker or database. Run it and read the trees it prints; README.md explains them.
//   dotnet run --project samples/Juice.Samples.Observability            sends two demo orders, then waits for yours
//   dotnet run --project samples/Juice.Samples.Observability -- --no-demo
// Set OTEL_EXPORTER_OTLP_ENDPOINT to also export everything to an OpenTelemetry Collector.

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls(builder.Configuration["urls"] ?? "http://localhost:5080");

var console = new TraceConsole();
var otlp = !string.IsNullOrEmpty(builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]);

// logs go only through OpenTelemetry, so each one is printed inside the span that wrote it
builder.Logging.ClearProviders();
builder.Logging.AddFilter("Microsoft", LogLevel.Warning).AddFilter("System", LogLevel.Warning);

builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource.AddService("juice-observability-sample"))
    .WithTracing(tracing =>
    {
        tracing
            .AddAspNetCoreInstrumentation()     // the HTTP server span (standard instrumentation)
            .AddJuiceInstrumentation()          // mediator, outbox and messaging spans
            .AddProcessor(console.Spans);
        if (otlp) tracing.AddOtlpExporter();
    })
    .WithMetrics(metrics =>
    {
        metrics
            .AddJuiceInstrumentation()          // juice.* metrics
            .AddJuiceDeliveryMetrics()          // delivery.metrics (local-channel delivery)
            .AddReader(new PeriodicExportingMetricReader(console.Metrics, exportIntervalMilliseconds: 3000));
        if (otlp) metrics.AddOtlpExporter();
    })
    .WithLogging(logging =>
    {
        logging
            .AddJuiceInstrumentation()          // Juice context on every log, payload redaction; before the exporters
            .AddProcessor(console.Logs);
        if (otlp) logging.AddOtlpExporter();
    }, options => options.IncludeFormattedMessage = true);

builder.Services.AddJuiceOpenTelemetry();       // mediator spans and metrics
builder.Services.AddMediatR(mediator => mediator.RegisterServicesFromAssemblyContaining<PlaceOrder>(includeNonPublicTypes: false));

var messaging = builder.Services.AddMessaging();
messaging.AddLocalChannel();
messaging.AddIdempotencyInMemory();
// IMessageService; with only local-channel routes nothing is written to a database, so no provider is configured
messaging.AddDefaultMessageService(_ => { });
builder.Services.AddSingleton<IMessagePublishingPolicy, LocalChannelPolicy>();
builder.Services.AddTransient<IIntegrationEventHandler<OrderPlaced>, OrderPlacedHandler>();

var app = builder.Build();

// initializes MessageContext (correlation id from x-correlation-id) and tags the HTTP server span with it
app.UseMessageContext("orders-api");
app.MapPost("/orders", async (string? number, bool? failBilling, IMediator mediator) =>
{
    await mediator.Send(new PlaceOrder(number ?? "A-1", failBilling == true));
    return Results.Accepted();
});

app.Lifetime.ApplicationStarted.Register(() => _ = StartedAsync(app.Urls.First(), runDemo: !args.Contains("--no-demo")));
app.Lifetime.ApplicationStopped.Register(console.Dispose);
await app.RunAsync();

async Task StartedAsync(string url, bool runDemo)
{
    console.WriteLine($"Listening on {url}. Each trace is printed once it has been quiet for a moment.", ConsoleColor.Green);
    if (runDemo)
    {
        using var http = new HttpClient { BaseAddress = new Uri(url) };

        // 1. an upstream service calls us: its traceparent makes our spans part of its trace
        using (var request = new HttpRequestMessage(HttpMethod.Post, "/orders?number=A-1"))
        {
            request.Headers.Add("traceparent", "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01");
            request.Headers.Add("x-correlation-id", "demo-order-A-1");
            await http.SendAsync(request);
        }
        await Task.Delay(1500);

        // 2. billing fails: the error shows on the spans, in the logs and in juice.messaging.errors
        using (var request = new HttpRequestMessage(HttpMethod.Post, "/orders?number=B-2&failBilling=true"))
        {
            request.Headers.Add("x-correlation-id", "demo-order-B-2");
            await http.SendAsync(request);
        }
        await Task.Delay(1500);
    }
    console.WriteLine($"""

        Send your own orders (Ctrl+C to stop):
          curl -X POST "{url}/orders?number=C-3" -H "x-correlation-id: my-order"
          curl -X POST "{url}/orders?number=D-4&failBilling=true"
        """, ConsoleColor.Green);
}
