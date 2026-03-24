# Messaging System — Usage Guidelines

Companion to `messaging-outbox.md` (architecture reference).
This file covers: route selection, IMessageService, MessageContext rules, DI setup, gotchas, testing.

---

## Route Selection

| Route | Publisher Key | Durability | Latency | Use When |
|-------|--------------|------------|---------|----------|
| `local-channel` | `"local-channel"` | None (in-memory) | Zero | Fire-and-forget in-process events; loss on restart is acceptable |
| `local` | `"local"` | Outbox-backed | Near-zero + retry | In-process but must survive restart/crash; needs idempotency |
| `rabbitmq` | `"rabbitmq"` | Broker | Network RTT | Cross-service; standard external events |

**Decision rule:**
- Need cross-service? → `rabbitmq`
- Need durability but same process? → `local`
- Just fan-out within request, no durability needed? → `local-channel`

Routes are config-driven via `PublishingPolicies` → `IMessagePublishingPolicy`. The same event class can fan-out to multiple routes simultaneously.

---

## IMessageService vs IOutboxService

```
IMessageService          — non-generic; handles "local-channel" route only; no DB dependency
IMessageService<TContext>— handles ALL routes (local-channel, local, rabbitmq); tied to a DbContext
IOutboxService<TContext> — lower-level; used by TransactionBehavior internally
```

**Use `IMessageService<TContext>`** when publishing from application services or command handlers that have a `DbContext` scope.
**Use `IMessageService`** (non-generic) only for fire-and-forget local-channel publishing with no transactional requirement.
**Do NOT inject `IOutboxService` directly** in application code — it's an infrastructure concern owned by `TransactionBehavior`.

```csharp
// Preferred: inject IMessageService<TContext> in services
public class OrderService(IMessageService<AppDbContext> messaging)
{
    public async Task PlaceOrderAsync(...)
    {
        // ... domain changes ...
        await messaging.PublishAsync(new OrderPlaced(...));
    }
}
```

---

## MessageContext — Initialization Rules

`MessageContext` is `AsyncLocal` — it must be **initialized at every async entry point**.
If not initialized, `MessageContext.Current` throws. Code that runs without HTTP context (background services, consumers, tests) must initialize it manually.

### Entry points that MUST initialize:
| Entry Point | How |
|-------------|-----|
| HTTP request | `MessageContextMiddleware` handles it (registered via `UseMessageContext()`) |
| RabbitMQ consumer | `RabbitMQConsumerEngine.Consumer_ReceivedAsync` initializes from message headers |
| `LocalTransportPublisher` delivery | Restores from outbox headers (`x-source`, `x-correlation-id`, `x-causation-id`) |
| xUnit tests | `[InitializeMessageContext]` attribute on test class |
| Background services | Must call `MessageContext.Initialize(...)` manually at the top of the loop iteration |

### Safe access pattern (when initialization is uncertain):
```csharp
// LocalDispatchHelper pattern — check before access
if (MessageContext.IsInitialized)
{
    var source = MessageContext.Current.Source;
}
else
{
    // fall back to empty/default
}
```

### Never:
- Access `MessageContext.Current` without first checking `IsInitialized` in background code
- Share a single `MessageContext` across parallel tasks (it's `AsyncLocal` — each branch gets a copy, which is correct)

---

## Local Transport Specifics

### "local-channel" route
- `LocalChannelBackgroundService` drains `Channel<IMessage>` and dispatches concurrently
- Handlers registered via `consumer.Subscribe<TEvent, THandler>()` in DI setup
- Handler resolution: `IntegrationEventDispatcher` first tries concrete type, then falls back to `IIntegrationEventHandler<T>` (this fallback is what local-channel uses)
- **Loss on restart**: nothing persisted — don't use for anything that must be reliably delivered

### "local" route
- Writes to outbox (same DB transaction), then immediately enqueues to channel for near-zero latency
- `LocalTransportPublisher` also handles delivery retry from outbox (via `DeliveryHostedService`)
- Idempotency key: `(EventName, "{Source}:{MessageId}")` — same key used by both immediate dispatch and retry path → `IntegrationEventDispatcher` deduplicates via `IIdempotencyService`
- If a message goes through both paths (immediate + retry), second delivery is silently skipped

### Double-enqueue guard
`IMessageService<TContext>` has a guard: if both `local-channel` and `local` routes are active for the same event, it skips the explicit channel enqueue for "local" to prevent double processing.

---

## DI Setup Patterns

### Minimal (local-channel only)
```csharp
services.AddMessaging()
    .AddLocalChannel(channel => {
        channel.AddConsumer(consumer => {
            consumer.Subscribe<MyEvent, MyEventHandler>();
        });
    });
services.AddSingleton<IMessageService, MessageService>();
```

### Full outbox + local + rabbitmq
```csharp
// Messaging core + outbox
services.AddMessaging()
    .AddPublishingPolicies(config.GetSection("PublishingPolicies"))
    .AddOutbox(outbox => {
        outbox.AddOutboxRepository();
        outbox.AddDeliveryIntents();
    });

// Local in-process transport
services.AddLocalTransport(local => {
    local.AddLocalChannel();
    local.AddLocalPublisher();          // registers ITransportPublisher keyed "local"
    local.AddConsumer(consumer => {
        consumer.Subscribe<MyEvent, MyEventHandler>();
    });
});
services.AddScoped<IMessageService<AppDbContext>, MessageService<AppDbContext>>();

// RabbitMQ transport
services.AddEventBus()
    .AddRabbitMQ(cfg => {
        cfg.AddProducer("rabbitmq", config.GetSection("RabbitMQ"));
        cfg.AddConsumer("my-exchange", "my-queue", "rabbitmq", consumer => {
            consumer.Subscribe<MyEvent, MyEventHandler>("my.routing.key");
        });
    });

// Background delivery
services.AddDelivery(delivery => {
    delivery.AddDeliveryProcessor<AppDbContext>("rabbitmq");
    delivery.AddDeliveryProcessor<AppDbContext>("local");
    delivery.AddDeliveryPolicies(config.GetSection("DeliveryPolicies"));
});
```

### OutboxProxy (when handler needs typed outbox interface)
```csharp
// Define a typed interface
public interface IMyOutbox : IOutboxService<AppDbContext> { }

// Register as proxy
services.AddOutboxProxy<IMyOutbox, AppDbContext>();

// Inject typed interface
public class MyHandler(IMyOutbox outbox) { ... }
```

---

## Publishing Policy Config

Events are routed by matching `(Domain, EventType, TenantTier)` against rules in priority order.

```json
{
  "PublishingPolicies": {
    "Default": {
      "Publishers": [{ "Key": "rabbitmq", "Destination": "default_exchange" }]
    },
    "Rules": [
      {
        "Priority": 10,
        "Match": { "Domain": "Orders" },
        "Publishers": [
          { "Key": "rabbitmq", "Destination": "orders_exchange" },
          { "Key": "local",    "Destination": "orders_local"   }
        ]
      }
    ]
  }
}
```

`[Domain("Orders")]` on the event class sets the domain for route resolution.
An event with no matching rule falls back to `Default`.
Multiple `Publishers` entries on one rule = fan-out to all simultaneously.

---

## Common Gotchas

### 1. MessageContext not initialized → NullReferenceException or InvalidOperationException
Add `[InitializeMessageContext]` to test classes. Add `UseMessageContext()` to the middleware pipeline. In background loops, call `MessageContext.Initialize(...)` at the start of each iteration.

### 2. Handlers not found for local-channel events
Local-channel uses `IIntegrationEventHandler<T>` interface resolution (not concrete type). Register handlers via `consumer.Subscribe<TEvent, THandler>()` — don't register them only by concrete type in DI.

### 3. OutboxRepository not sharing transaction
If your `DbContext` doesn't implement `IOutboxContext`, `OutboxRepository` must share the connection via `EnsureAssociatedConnection()`. This is handled internally — but if you use a custom `IOutboxRepository`, you must call it explicitly.

### 4. Double delivery on "local" route
If you both immediately dispatch AND the delivery service retries (e.g., after a crash during immediate dispatch), the second delivery is idempotency-deduplicated. Ensure `IIdempotencyService` is registered or messages will be processed twice.

### 5. TransactionBehavior owns SaveOutbox — don't call SaveEventsAsync manually
`OutboxEventService.SaveEventsAsync` is called by `TransactionBehavior` step (e) inside the resilient transaction. Calling it outside breaks atomicity. Application code adds events via `IOutboxService.AddEventAsync`; the behavior commits them.

### 6. Background services registering duplicate IModuleStartup
If multiple assemblies each have a startup that registers a `DeliveryHostedService`, they'll produce duplicate background services for the same Publisher×Intent. Use one delivery registration point (usually the host startup).

### 7. Delivery policy key format
`"PublisherKey:IntentName:ContextTypeName"` — all three segments required for specific config. Falls back to `"default"` if not found. `ContextTypeName` is the short type name (e.g., `AppDbContext`, not the full namespace).

---

## Testing Patterns

### Unit test with MessageContext
```csharp
[InitializeMessageContext]           // attribute initializes MessageContext per test
public class MyHandlerTests
{
    [Fact]
    public async Task Handle_publishes_event_Async()
    {
        // arrange: use real or mock IMessageService
    }
}
```

### Infrastructure-dependent tests (RabbitMQ, DB)
Use `IgnoreOnCIFact` instead of `[Fact]` — skips on CI where infrastructure is unavailable:
```csharp
[IgnoreOnCIFact]
public async Task Delivery_reaches_consumer_Async() { ... }
```

### Testing route resolution
Inject `IMessagePublishingPolicy` directly and call `ResolveAsync(new PolicyResolveContext(...))` — no message infrastructure needed.

### Testing outbox accumulation
Use `IOutboxService.AddEventAsync` then verify `OutboxEvent` rows in a real or in-memory DB. Do NOT mock `OutboxRepository` — it has DB-specific behavior (connection sharing).

---

## Key Files Quick Reference

| Concern | File |
|---------|------|
| Unified publish interface | `core/src/Juice.Messaging/IMessageService.cs` |
| All-routes impl | `core/src/Juice.Messaging.Local/Internal/MessageServiceT.cs` |
| Local-channel only impl | `core/src/Juice.Messaging.Local/Internal/MessageService.cs` |
| Channel drain + dispatch | `core/src/Juice.Messaging.Local/Internal/LocalChannelBackgroundService.cs` |
| Outbox-backed "local" delivery | `core/src/Juice.Messaging.Local/Internal/LocalTransportPublisher.cs` |
| MessageContext safety | `core/src/Juice.Messaging.Local/Internal/LocalDispatchHelper.cs` |
| Handler resolution fallback | `core/src/Juice.Messaging/Integrations/IntegrationEventDispatcher.cs` |
| Full lifecycle (TransactionBehavior) | `core/src/Juice.MediatR.Behaviors/TransactionBehavior.cs` |
| Outbox accumulation | `core/src/Juice.Messaging.Outbox/Internal/OutboxEventService.cs` |
| Background delivery loop | `core/src/Juice.Messaging.Outbox.Delivery/Processing/DeliveryHostedService.cs` |
| Full integration test | `core/test/Juice.Integrations.Tests/TransactionBehaviorTest.cs` |
| Idempotency dedup test | `core/test/Juice.Messaging.Local.Tests/IdempotencyDeduplicationTests.cs` |
