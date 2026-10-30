# Juice observability sample

A small web app that shows what Juice records with OpenTelemetry. It prints every **trace** as a tree, with the
**logs** written inside each span nested under it, and a **metrics** summary whenever the numbers change. It needs
no broker or database: the event travels over the in-process `local-channel` transport.

```
dotnet run --project samples/Juice.Samples.Observability
```

On start it sends two demo orders (one succeeds, one fails in billing), then waits for yours:

```
curl -X POST "http://localhost:5080/orders?number=C-3" -H "x-correlation-id: my-order"
curl -X POST "http://localhost:5080/orders?number=D-4&failBilling=true"
```

`--no-demo` skips the demo orders. Set `OTEL_EXPORTER_OTLP_ENDPOINT` (for example `http://localhost:4318`) to
also export everything to an OpenTelemetry Collector, as a real host would.

## The flow

One process plays two services:

```
orders:   POST /orders → mediator PlaceOrder → publish OrderPlaced
billing:  consume OrderPlaced → OrderPlacedHandler → mediator CreateInvoice
```

## Reading a trace

```
══ trace 4bf92f3577b34da6a3ce929d0e0e4736  correlation=demo-order-A-1  (6 spans, 3 logs)
   (continues remote parent span 00f067aa from the traceparent header)
└─ POST /orders  [ASP.NET Core · server]  285.7 ms  span=a5d15474  http.request.method=POST url.path=/orders http.response.status_code=202
   └─ mediator PlaceOrder  [Juice · internal]  21.0 ms  span=cf1be195
      ├· log Information PlaceOrderHandler: Placing order A-1   (juice.correlation_id=demo-order-A-1 juice.causation_id=-)
      └─ send OrderPlaced  [Juice · producer]  4.2 ms  span=335f0d08  messaging.system=local-channel
         └─ process OrderPlaced  [Juice · consumer]  7.2 ms  span=a951494f  messaging.system=local-channel juice.dispatch.result=success
            └─ handle OrderPlacedHandler  [Juice · internal]  1.2 ms  span=17a64503  juice.handler=...OrderPlacedHandler
               ├· log Information OrderPlacedHandler: Billing order A-1   (juice.correlation_id=demo-order-A-1 juice.causation_id=c07sd836...)
               └─ mediator CreateInvoice  [Juice · internal]  0.3 ms  span=3c4633b0
                  └· log Information CreateInvoiceHandler: Invoice created for A-1   (...)
```

- **One trace id for the whole flow.** Every span and log of the order shares it, across the HTTP request, the
  mediator, the message and the consumer. That id is what a backend (Grafana, Jaeger...) uses to show the flow.
- **The tree is the parent/child links.** Each span records its parent's span id; the printer nests on that.
  `process OrderPlaced` is a child of `send OrderPlaced` because the message carried the trace context in its
  `traceparent` header. Over RabbitMQ or NATS the consumer is another process, and that header is the only link.
- **Who records what.** `[ASP.NET Core · server]` comes from the standard ASP.NET Core instrumentation; Juice does not
  add a second HTTP span. `[Juice · ...]` spans come from Juice: `mediator` for each mediator request, `send` /
  `process` / `handle` for messaging (with the outbox and a broker you also see `create` and `deliver`).
  Database spans would come from the SqlClient or EF Core instrumentation, never from Juice.
- **The kind** says the span's role: `server` handles an incoming request, `producer` sends a message, `consumer`
  receives one, `internal` is work inside the process.
- **Remote parent.** The first demo request sends a `traceparent` header, as an upstream service would; the trace
  keeps the caller's trace id (`4bf92f35...`) and the HTTP span's parent is the caller's span (`00f067aa`).
- **Logs are inside spans.** OpenTelemetry stamps each log with the current trace and span id, which is how it
  lands under the right span here and how a backend links a log to its trace. The Juice log processor adds the
  `MessageContext` ids: `juice.correlation_id` (from the `x-correlation-id` header, the same for the whole flow) and
  `juice.causation_id` (empty for the HTTP request; in the consumer it is the id of the message that caused the
  work).

## Reading a failure

```
└─ process OrderPlaced  [Juice · consumer]  ...  juice.dispatch.result=failure  ERROR: One or more handlers failed
   ├─ handle OrderPlacedHandler  [Juice · internal]  ...  ERROR: No billing account for order B-2
   │  ! exception System.InvalidOperationException: No billing account for order B-2
   │  ├─ mediator CreateInvoice  [Juice · internal]  ...  ERROR: No billing account for order B-2
   │  │  ! exception System.InvalidOperationException: No billing account for order B-2
   │  └· log Error IntegrationEventDispatcher: OrderPlacedHandler failed to handle event ...
   └· log Warning LocalChannelBackgroundService: Dispatch of OrderPlaced ... failed ... remains NotPublished for retry.
```

The span where the exception was thrown and every Juice span above it are marked `ERROR`, with an `exception`
event. The HTTP request still returned `202`: billing runs after the response, in the consumer.

## Reading the metrics

Metrics are counts and durations, not per-request records, and they never carry ids (no correlation or message
ids), so they stay cheap to store. Values are cumulative since the app started.

| Metric | Meaning |
|---|---|
| `juice.mediator.requests` / `.errors` / `.request.duration` | mediator requests by type (`juice.operation`), failures by `error.type` |
| `juice.messaging.published` | messages published, by transport and message type |
| `juice.messaging.consumed` / `.process.duration` | messages consumed, by `juice.dispatch.result` |
| `delivery.metrics` `delivery_*` | delivery attempts, outcomes and latency per publisher (from `AddJuiceDeliveryMetrics()`) |

## The setup in `Program.cs`

| Call | Adds |
|---|---|
| `WithTracing(t => t.AddAspNetCoreInstrumentation())` | the HTTP server span |
| `WithTracing(t => t.AddJuiceInstrumentation())` | the Juice spans |
| `services.AddJuiceOpenTelemetry()` | the mediator spans and `juice.mediator.*` metrics |
| `WithMetrics(m => m.AddJuiceInstrumentation().AddJuiceDeliveryMetrics())` | the Juice metrics |
| `WithLogging(l => l.AddJuiceInstrumentation())` | the Juice context on logs and payload redaction (before any exporter) |
| `app.UseMessageContext("orders-api")` | the `MessageContext` for each request, copied onto the HTTP span |

`TraceConsole.cs` is the printer: an OpenTelemetry span processor, log processor and metric exporter. It is for
learning only; a real host adds an exporter such as `AddOtlpExporter()` instead.
