# Messaging & Outbox Pipeline

## Overview
The Outbox pattern ensures messages are published atomically with business data.
Messages are saved to DB in the same transaction as the domain change, then delivered
by a background service.

---

## Key Interfaces & Classes

### Message Contracts
```
IMessage              (Juice.Contracts)  — base: MessageId (Guid), CreatedAt
IEvent : IMessage     (Juice.Contracts)  — adds EventName
IntegrationEvent      (EventBus.Contracts) — base record: MessageId, CreatedAt
```

### Outbox Write Side
```
IOutboxService                         — AddEventAsync(IMessage), SaveEventsAsync(Guid? txId, CT)
IOutboxService<TContext> : IOutboxService  — marker for DI keying
OutboxEventService<TContext>           — internal impl; accumulates messages, resolves routes, saves to DB
```

### Outbox Storage
```
OutboxEvent            — EventId, EventTypeName, PayloadBytes, Headers, TransactionId, TenantId, Deliveries[]
OutboxDelivery         — DeliveryId, EventId, PublisherKey, Destination, State, RetryCount, NextAttemptOn
DeliveryState          — NotPublished | InProgress | Published | Failed | Skipped
IOutboxRepository      — SaveEventsAsync, MarkAsPublishedAsync, MarkAsInProgressAsync, MarkAsFailedAsync, MarkAsSkippedAsync
IOutboxRepository<T>   — generic marker for DI
OutboxRepository<TContext> — EF Core impl; shares DB connection/transaction with domain context
IOutboxContext         — interface a DbContext must implement to hold Outbox + OutboxDeliveries DbSets
```

### Publishing Policy (Route Resolution)
```
IMessagePublishingPolicy    — ResolveAsync(PolicyResolveContext) → IReadOnlyCollection<PublishRoute>
PolicyResolveContext        — EventType, Domain, TenantIdentifier, TenantTier
PublishRoute                — record(PublisherKey, Destination)
DefaultEventPublishingPolicy — reads PublishingPolicyOptions (config-driven rules + priority)
[Domain("Orders")]          — attribute on event class → sets domain name for routing
```

### Delivery (Background Processing)
```
DeliveryHostedService<TContext>   — BackgroundService; one instance per Publisher×Intent combo
DeliveryProcessor<TContext>       — does MarkInProgress → PublishAsync → MarkPublished/Failed
IDeliveryIntent<TContext>         — RetrieveDeliveriesAsync(publisherKey, policy, ct) → OutboxDelivery[]
DeliveryPolicy                    — record: Interval, BatchSize, Timeout, InitialRetryDelay, RetryDelayMultiplier, MaxRetryAttempts
IDeliveryPolicyProvider           — provides delivery policies
IDeliveryPolicyResolver           — resolves policy for DeliveryContext(publisher, intent, contextName)
```

### Built-in Intents (registered as keyed services)
| Key | Class | Behavior |
|-----|-------|---------|
| `send-pending` | `SendPendingIntent<T>` | Queries State=NotPublished, ordered by CreationTime |
| `retry-failed` | `RetryFailedIntent<T>` | State=Failed AND NextAttemptOn < now |
| `recover-timeout` | `RecoverTimeoutIntent<T>` | State=InProgress longer than Timeout (stuck); `DeliveryProcessor` claims these via `ReclaimInProgressAsync` (CAS on ProcessedOn) instead of `MarkAsInProgressAsync`; each reclaim increments RetryCount and past `MaxRetryAttempts` the delivery is marked Failed (no next attempt). `ProcessedOn`/`NextAttemptOn` use the app clock (UTC) |

### Transport (Broker)
```
ITransportPublisher        — Key (string), PublishAsync(byte[], PublishContext, CT)
RabbitMQProducer           — implements ITransportPublisher; channel pool with publisher confirms (tracking): PublishAsync completes
                             only once the broker has the message and throws on nack, basic.return (only with Mandatory(), off by default)
                             or a channel close (404 no exchange) → the outbox marks the delivery Failed and retries it.
                             Async Polly retry (MaxRetryAttempts, RetryBaseDelay 0.2s doubling) only for dropped
                             connections/channels; 403/404/406 and refusals are not retried. Routing key: PublishContext.RoutingKey
                             (policy route) → x-routing-key → x-message-name → x-message-type (same as NATS)
DefaultRabbitMQPersistentConnection — first connect with async retries (ConnectionMaxRetries); after that the client's
                             automatic recovery owns reconnects (no second connection while recovering). Shared keyed
                             singleton: consumers do not dispose it on stop
JetStreamProducer          — implements ITransportPublisher (Juice.EventBus.NATS, key "nats"); see "NATS JetStream transport"
PublishContext             — MessageId, TenantId, Destination (exchange), Headers, RoutingKey
```

### OutboxProxy
```
OutboxProxy<T>             — DispatchProxy impl; wraps IOutboxService<TContext> as typed TOutbox
MessagingBuilder.AddOutboxProxy<TOutbox, TContext>()  — registers the proxy
```

---

## Full Message Lifecycle

### Phase 1: Command Handling (TransactionBehavior)
```
MediatR.Send(Command)
  → TransactionBehavior.Handle()
    0. Already managed (IsManaged/HasActiveTransaction) → nested command: just next(), joins outer transaction
    1. BeginManage() — notify DbContext it's being managed (repository/UoW writes are staged, not saved)
    2. next() — run handler (domain changes, raise domain events)
    3. ResilientTransaction.ExecuteAsync():
       a. SaveChangesAsync() — persist domain entities
       b-d. DispatchEventsUntilStableAsync() — domain, audit, data change events in rounds;
            SaveChanges between rounds until handlers raise no new events (max 10 rounds)
       e. outboxService.SaveEventsAsync(transaction.TransactionId)
          → OutboxEventService: resolves routes via IMessagePublishingPolicy
          → Builds OutboxEvent + OutboxDelivery[] rows
          → OutboxRepository.SaveEventsAsync() — EF insert (same DB connection/transaction)
       f. CommitTransactionAsync() — commit everything atomically
       g. ClearEvents()
    4. return response (finally: EndManage())
```

### Phase 2: Background Delivery (DeliveryHostedService)
```
DeliveryHostedService<TContext> loop (per Publisher × Intent):
  1. Resolve DeliveryPolicy from IDeliveryPolicyResolver
  2. Every policy.Interval:
     a. Create new DI scope
     b. Resolve IDeliveryIntent<TContext> (keyed by intent name)
     c. Resolve ITransportPublisher (keyed by publisher name)
     d. deliveries = intent.RetrieveDeliveriesAsync(publisher, policy, ct)
     e. DeliveryProcessor.ProcessAsync(deliveries):
        - MarkAsInProgressAsync (optimistic lock: rowcount check)
        - ITransportPublisher.PublishAsync(payload, context) → RabbitMQProducer
        - MarkAsPublishedAsync  OR  MarkAsFailedAsync(nextAttempt)
```

### Phase 3: Consumption (RabbitMQConsumerEngine)
```
RabbitMQConsumerEngine.Consumer_ReceivedAsync():
  1. Extract routingKey from x-original-routing-key header
  2. Lookup eventType from ISubscriptionsManager by routingKey
     (supports topic wildcard matching: * = one segment, # = zero+)
  3. Deserialize message bytes → IIntegrationEvent
  4. Initialize MessageContext (correlationId, causationId=messageId, executionId, source)
  5. Resolve tenant context via IScopedTenantResolver (from x-tenant-id header)
  6. IntegrationEventDispatcher.DispatchAsync():
     a. Check IIdempotencyService (key: "{EventName}:{Source}:{MessageId}")
     b. If duplicate → return Duplicated (skip)
     c. Resolve handler types from ISubscriptionsManager
     d. Create DI scope per dispatch
     e. For each handler: resolve from DI, invoke HandleAsync via reflection cache
     f. Return: Success (≥1 handler ok) | Failure (all failed) | NotHandled (no handlers)
  7. Result handling:
     - Success/Duplicated → BasicAckAsync (remove from queue)
     - Failure → Check IRetryPolicyProvider:
       - Retries remaining → republish to retry exchange with updated x-attempts header (consumer channel has
         publisher confirms: the original is acked only after the copy is confirmed; a refused copy → nack requeue after 1s)
       - Max retries reached + parking enabled → route to parking queue (DLQ)
       - Max retries reached + no parking → BasicNackAsync
     - NotHandled → BasicNackAsync
```

### Consumer Retry Topology (RabbitMQ)
```
Main exchange (direct) → main queue (consumer listens here)
  on failure → retry exchange (topic)
    → retry.10s queue (TTL 10s, DLX → main exchange)
    → retry.1m   queue (TTL 1m,  DLX → main exchange)
    → retry.5m   queue (TTL 5m,  DLX → main exchange)
  on max retries → parking exchange → parking queue (no consumer, manual intervention)
```
Additional headers added on retry/parking:
| Header | Value |
|--------|-------|
| `x-attempts` | Retry count (incremented) |
| `x-original-exchange` | Source exchange |
| `x-original-routing-key` | Original routing key |
| `x-death-reason` | Why message was parked |
| `x-death-timestamp` | When it was parked |
| `x-original-queue` | Queue it came from |

### EventBus (Direct Publishing, non-Outbox)
`IEventBus` → `CompositeEventPublisher` — for direct event publishing (no outbox atomicity):
```csharp
// Resolves routes via IMessagePublishingPolicy, adds standard headers, publishes via keyed ITransportPublisher
await eventBus.PublishAsync(event, domain: "Orders");
```
Relationship: Outbox uses `ITransportPublisher` directly (bypass IEventBus). IEventBus is for non-transactional direct publishing.

### Subscriptions
- `ISubscriptionsManager` — in-memory registry of event→handler mappings
- `SubscriptionInfo` — metadata: EventType, HandlerType, Key, IsDynamic
- `RoutingKeyUtils.IsTopicMatch()` — RabbitMQ topic wildcard matching (regex-based)
- Registration: `consumer.Subscribe<TEvent, THandler>(routingKey?)` during DI setup

---

## Message Headers (carried in OutboxEvent.Headers)
| Header | Value |
|--------|-------|
| `x-correlation-id` | From MessageContext |
| `x-causation-id` | MessageContext.ExecutionId |
| `x-source` | MessageContext.Source |
| `x-tenant-id` | Current tenant ID |
| `x-message-type` | Event class name |
| `x-message-id` | Event Guid |
| `x-message-name` | EventName or type name (RabbitMQ routing key unless the route sets one) |
| `x-original-exchange` | Exchange where message was first published |
| `x-original-routing-key` | Routing key used on publish |
| `x-attempts` | Retry count (consumer side) |
| `traceparent` / `tracestate` | W3C trace context (10.6): the outbox create span, or the producer send span for direct `IEventBus` publishes. Never overwritten once set |

## Tracing (10.6, `Juice.Messaging/Diagnostics`)
- One `ActivitySource` named `"Juice"` (`JuiceActivitySource`), BCL `System.Diagnostics` only, no OpenTelemetry package in core. Hosts collect with `AddSource(JuiceActivitySource.Name)`.
- `MessagingActivity` starts/tags every span and does `Inject`/`Extract` through `DistributedContextPropagator.Current` (only `traceparent`/`tracestate`, no baggage). All methods are null-safe and never throw.
- Spans: `create {Type}` (Producer, `OutboxEventService.SaveEventsAsync`, context stored in `OutboxEvent.Headers`) → `deliver {publisherKey}` (Internal, `DeliveryProcessor`, parent = stored context) → `send {exchange|subject}` (Producer, `RabbitMQProducer`/`JetStreamProducer`/`LocalChannelMessagePublisher`) → `process {queue|destination|Type}` (Consumer: RabbitMQ/NATS engines, `LocalTransportPublisher`, `LocalChannelBackgroundService`) → `handle {Handler}` (Internal, `IntegrationEventDispatcher`).
- `process` parent = extracted header context, or `Activity.Current` when it is in the same trace (in-process `local` delivery nests under `deliver`). `local-channel` carries the context in `ChannelEnvelope.TraceContext` (the reader loop has no `Activity.Current`).
- `MessageContext` ids are tags (`juice.correlation_id`, `juice.causation_id`, `juice.execution_id`, `juice.source`), never the trace parent. Tests: `Juice.XUnit.ActivityCollector` (plain `ActivityListener`, filter by trace id).
- Metrics (10.7): `JuiceMeter` (`"Juice"`) + `MessagingMetrics` record `juice.messaging.*` / `juice.outbox.*` at the same call sites as the spans. Bounded dimensions only (no message/tenant/correlation ids). Old meters (`delivery.metrics`, `infra.*`) kept for dashboards until 11.0.
- HTTP (10.7): `MessageContextMiddleware`/`[MessageContext]` tag `Activity.Current` (the ASP.NET Core server span); never start an HTTP span.
- `Juice.OpenTelemetry` (10.7, net8+, `OpenTelemetry.Api` only): `AddJuiceInstrumentation()` on tracer/meter builders = `AddSource`/`AddMeter("Juice")`; `services.AddJuiceOpenTelemetry()` registers mediator behaviors at `Order = int.MinValue` (idempotency moved to `int.MinValue + 10`). Tests in `core/test/Juice.OpenTelemetry.Tests` use the real SDK + in-memory exporters, parallelization disabled (providers listen process wide). `CrossProcessTraceTests` starts `core/test/Juice.OpenTelemetry.TestConsumer` (separate OS process, `dotnet <dll>` from `build/bin/<cfg>/Juice.OpenTelemetry.TestConsumer/<tfm>/`, RabbitMQ settings via env vars, spans exported as JSON lines to a temp file, stops when stdin closes) and asserts its `process` span's parent is the producer's `create` span.

---

## DI Setup Example
```csharp
// 1. Setup messaging + outbox
services.AddMessaging()
    .AddPublishingPolicies(config.GetSection("PublishingPolicies"))
    .AddOutbox(outbox => {
        outbox.AddOutboxRepository();  // registers OutboxRepository<>
        outbox.AddDeliveryIntents();   // registers send-pending, retry-failed, recover-timeout
    });

// 2. Setup delivery processor (background service)
services.AddDelivery(delivery => {
    delivery.AddDeliveryProcessor<MyDbContext>("rabbitmq");
    delivery.AddDeliveryPolicies(config.GetSection("DeliveryPolicies"));
});

// 3. Setup RabbitMQ transport
services.AddEventBus()
    .AddRabbitMQ(cfg => {
        cfg.AddProducer("rabbitmq", config.GetSection("RabbitMQ"));
        cfg.AddConsumer("exchange", "queue", "rabbitmq", consumer => {
            consumer.Subscribe<MyEvent, MyEventHandler>();
        });
    });

// 4. Register outbox proxy (optional — when handler needs typed IOutboxService)
services.AddOutboxProxy<IMyOutboxService, MyDbContext>();
```

---

## Configuration Schema (appsettings.json)
```json
{
  "PublishingPolicies": {
    "Default": {
      "Publishers": [{ "Key": "rabbitmq", "Destination": "default_exchange" }]
    },
    "Rules": [
      {
        "Priority": 10,
        "Match": { "Domain": "Orders", "EventType": "OrderCreated" },
        "Publishers": [{ "Key": "rabbitmq", "Destination": "orders_exchange" }]
      }
    ]
  },
  "DeliveryPolicies": {
    "rabbitmq:send-pending:MyDbContext": {
      "Interval": "00:00:05",
      "BatchSize": 10,
      "InitialRetryDelay": "00:00:05",
      "RetryDelayMultiplier": 2.0,
      "MaxRetryAttempts": 3
    },
    "default": {
      "InitialRetryDelay": "00:00:05",
      "RetryDelayMultiplier": 2.0,
      "MaxRetryAttempts": 3
    }
  }
}
```
**DeliveryPolicy config key format**: `"PublisherKey:IntentName:ContextTypeName"` — supports wildcards/fallback to `"default"`.

---

## Database Schema (EF entity configurations)
Table: `OutboxEvents` — stores the serialized message + headers + TransactionId
Table: `OutboxDeliveries` — one row per publisher/destination route per event

**Key Indexes on OutboxDeliveries** (critical for performance):
| Index | Columns | Used By |
|-------|---------|---------|
| `IX_Pending` | State=NotPublished, CreationTime | SendPendingIntent |
| `IX_Retry` | State=Failed, NextAttemptOn IS NOT NULL | RetryFailedIntent |
| `IX_Recovery` | State=InProgress, ProcessedOn | RecoverTimeoutIntent |

## MessageSerializer Security
`MessageSerializer` uses Newtonsoft.Json with `TypeNameHandling.All` + `KnownTypesBinder`
which whitelists only `Juice.*` assemblies on deserialization — prevents type-confusion attacks.

## Important: OutboxRepository Connection Sharing
When `TContext` does NOT implement `IOutboxContext`, a separate `IOutboxContext`
DbContext is used. `OutboxRepository.EnsureAssociatedConnection()` shares the DB
connection AND transaction via `Database.SetDbConnection()` + `Database.UseTransaction()`.
This is how atomicity is achieved across two DbContext instances.

---

## NATS JetStream transport (`Juice.EventBus.NATS`, 10.5)
Second broker transport, same folder layout as `Juice.EventBus.RabbitMQ`. Spec/contracts: `specs/012-nats-jetstream/`.
No outbox/delivery/dispatcher code changed — the producer is just another keyed `ITransportPublisher`.

### Naming
| Item | Rule | Example |
|---|---|---|
| Subject | `{destination}.{routingKey}` (routing key: `PublishContext.RoutingKey` → `x-routing-key` → `x-message-name` → `x-message-type`) | `content.ContentPublishedIntegrationEvent` |
| Stream | destination upper-cased, `.`/`-`/space → `_`, subjects `{destination}.>` | `CONTENT` |
| Parking stream | `{STREAM}_PARKING`, subjects `parking.{destination}.>` (outside the main stream's subjects) | `CONTENT_PARKING` |
| Filter subject | route `kernel.*` → `{d}.kernel.*`; trailing `#` → `>`; non-trailing `#` → `{d}.>` + in-process topic match | |
| `Nats-Msg-Id` | `{PublishContext.MessageId}:{subject}` (event id per subject) → JetStream de-duplication within `DuplicateWindow` (10 min default) | |

Headers are strings on the wire (`JetStreamHeaders`): `byte[]` → UTF-8, dates → ISO "O", others invariant `ToString()`.

### Publish
`JetStreamProducer.PublishAsync` completes only after the stream ack; a `Duplicate` ack is success (outbox retry after a
lost ack). No stream for the subject → `InvalidOperationException` naming subject + destination.

### Consume (differs from RabbitMQ)
- Durable **pull** consumer per `AddConsumer(key, stream, durableName, connection)`; instances sharing `durableName` share work.
- `JetStreamConsumerEngine.HandleAsync`: event name from `x-original-routing-key` ?? `x-message-name` ?? subject suffix;
  `MessageContext` initialized/cleared per message; `AckProgress` every `AckWait/2` while the handler runs.
- **Retry = delayed NAK**, attempt = server `NumDelivered` (no retry exchanges/TTL queues). Tiers come from the shared
  `ConsumeRetryPolicyOptions` with `Source` = consumer destination; tier strings `10s` or RabbitMQ-style `content.retry.10s`.
- Max reached + parking → publish copy to `{DeadLetterDest ?? parking.{destination}}.{event}` with `x-death-*` headers, then ACK.
  Parking off / no policy → `AckTerminate`. Not deserializable → park (if policy parking on) else terminate.
  `NotHandled` → `OnNotHandled(Terminate|Park)`, default terminate.
- Unexpected processing exception → NAK with a fixed 5 s (`FallbackRedeliveryDelay`). `AddConsumer` always registers
  `IRetryPolicyProvider<JetStreamRetryPolicy>`; with no `Default`/matching policy, a handler failure is terminated (as RabbitMQ nacks).
- Test pitfall: `Juice.XUnit.TestOutputLogger` scopes are not thread-safe; concurrent `BeginScope` can throw, which the
  engine turns into a 5 s NAK (visible as "Index was out of range" warnings in integration test output).
- The consume loop never blocks on the client enumerator; concurrency is bounded inside each handler task
  (`MaxConcurrency`) and server-side by `MaxAckPending = MaxConcurrency * 2`.
- Topology: `Provisioning(VerifyOnly)` (default) fails fast on a missing stream/consumer; `CreateIfMissing` creates.
  Existing streams/consumers are never updated (filter mismatch is only logged).

### DI
```csharp
services.AddEventBus()
    .AddConsumerRetryPolicies(config.GetSection("ConsumeRetryPolicies"))
    .AddJetStream(js => js
        .AddConnection("nats", config.GetSection("Juice:EventBus:Connections:Nats"))
        .AddProducer("nats", "nats")
        .AddInfrastructureTopology("nats", t => t.DeclareStream("content").DeclareParkingStream("content"))
        .AddConsumer("content-worker", "CONTENT", "content-worker", "nats", c => c
            .Destination("content")
            .Subscribe<ContentPublishedIntegrationEvent, ContentPublishedHandler>()));
services.AddHealthChecks().AddJetStreamHealthCheck("nats");   // Open=Healthy, Reconnecting=Degraded
await app.InitJetStreamInfrastructureAsync();
```

### Testing
`core/test/Juice.EventBus.NATS.Tests`: `Unit/` is CI-safe; `Integration/` needs `nats-server -js` on :4222 and, for outbox
end-to-end tests, SQL Server LocalDB (the outbox delivery queries do not translate on SQLite).

---

## Idempotency (Consumer Side)
```
IIdempotencyService        — TryBeginRequestAsync, TryCompleteRequestAsync
Implementations:
  - InMemoryIdempotencyService
  - DistributedCacheIdempotencyService
  - RedisIdempotencyService
  - EF IdempotencyService (SQL Server / PostgreSQL)
```
Used via `IdempotencyRequestBehavior<TRequest>` in the MediatR pipeline on consumer side.
