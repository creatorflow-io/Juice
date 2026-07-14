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
| `recover-timeout` | `RecoverTimeoutIntent<T>` | State=InProgress longer than Timeout (stuck) |

### Transport (Broker)
```
ITransportPublisher        — Key (string), PublishAsync(byte[], PublishContext, CT)
RabbitMQProducer           — implements ITransportPublisher; uses channel pool, Polly retry
PublishContext             — MessageId, TenantId, Destination (exchange), Headers
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
    1. BeginManage() — notify DbContext it's being managed
    2. next() — run handler (domain changes, raise domain events)
    3. ResilientTransaction.ExecuteAsync():
       a. SaveChangesAsync() — persist domain entities
       b. DispatchDomainEventsAsync() — in-process domain event handlers
       c. DispatchAuditEventsAsync()  — audit tracking
       d. DispatchDataChangeEventsAsync()
       e. outboxService.SaveEventsAsync(transaction.TransactionId)
          → OutboxEventService: resolves routes via IMessagePublishingPolicy
          → Builds OutboxEvent + OutboxDelivery[] rows
          → OutboxRepository.SaveEventsAsync() — EF insert (same DB connection/transaction)
       f. CommitTransactionAsync() — commit everything atomically
       g. ClearEvents()
    4. return response
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
       - Retries remaining → republish to retry exchange with updated x-attempts header
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
| `x-message-name` | EventName or type name (used as RabbitMQ routing key) |
| `x-original-exchange` | Exchange where message was first published |
| `x-original-routing-key` | Routing key used on publish |
| `x-attempts` | Retry count (consumer side) |

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
