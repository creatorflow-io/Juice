# Dependency Injection Patterns

## Builder Pattern Convention
Every major subsystem uses a fluent builder registered via extension methods on IServiceCollection.
Builders hold `IServiceCollection Services` and return `this` for chaining.

### MessagingBuilder
```csharp
// Entry point
services.AddMessaging(builder => {
    builder.AddPublishingPolicies(config.GetSection("PublishingPolicies"));
    builder.AddOutboxProxy<IMyOutboxService, MyContext>();
});
// Adds: IMessageSerializer (singleton), IMessagePublishingPolicy (singleton)
```

### OutboxBuilder (via MessagingBuilder.AddOutbox)
```csharp
services.AddMessaging()
    .AddOutbox(outbox => {
        outbox.AddOutboxRepository();   // scoped IOutboxRepository<>
        outbox.AddDeliveryIntents();    // keyed scoped IDeliveryIntent<>
    });
// Also calls AddOutboxCore() which registers IOutboxService<TContext>
```

### DeliveryBuilder (via services.AddDelivery)
```csharp
services.AddDelivery(delivery => {
    delivery.AddDeliveryProcessor<MyDbContext>("rabbitmq");
    // or with config:
    delivery.AddDeliveryProcessor<MyDbContext>("rabbitmq", builder => {
        builder.WithIntents("send-pending", "retry-failed");
    });
    delivery.AddDeliveryPolicies(config.GetSection("DeliveryPolicies"));
});
// Registers: DeliveryHostedService<T> (hosted service per publisher×intent)
// DeliveryProcessor<T> (scoped), IDeliveryPolicyResolver (singleton)
```

### EventBusBuilder (via services.AddEventBus)
```csharp
services.AddEventBus()
    .AddRabbitMQ(cfg => {
        cfg.AddConnection("rabbitmq", config.GetSection("RabbitMQ"));
        cfg.AddProducer("rabbitmq", endpoint => { ... });
        cfg.AddConsumer("exchange", "queue", "rabbitmq", consumer => {
            consumer.Subscribe<MyEvent, MyHandler>();
        });
    });
// Registers: IEventBus (singleton), keyed ITransportPublisher, ISubscriptionsManager
// Consumer hosted service
```

### MediatorBuilder (via services.AddMediatR)
```csharp
services.AddMediatR(cfg => {
    cfg.RegisterServicesFromAssembly(typeof(MyHandler).Assembly);
    cfg.RegisterServicesFromAssembly(typeof(MyEventHandler).Assembly, scanNotifications: true);
})
.AddOperationLoggingBehavior()
.AddIdempotencyRequestBehavior(b => {
    b.Messaging.AddIdempotencyRedis(opts => { opts.ConnectionString = "..."; });
});
// Note: Juice.MediatR uses its own IMediator, NOT MediatR's. Registration helpers exist for compatibility.
```

---

## Key DI Conventions

### Scoped vs Singleton
| Service | Lifetime | Reason |
|---------|----------|--------|
| `IOutboxService<T>` | Scoped | Accumulates messages per request |
| `IOutboxRepository<T>` | Scoped | EF DbContext is scoped |
| `IDeliveryIntent<T>` | Scoped (keyed) | Uses DbContext |
| `DeliveryProcessor<T>` | Scoped | Uses DbContext via IOutboxRepository |
| `IMessagePublishingPolicy` | Singleton | Reads config once |
| `IMessageSerializer` | Singleton | Stateless |
| `IEventBus` | Singleton | Connection pool |
| `ITransportPublisher` | Singleton (keyed) | Channel pool per publisher |
| `IDeliveryPolicyResolver` | Singleton | Config-driven |

### Keyed Services (Microsoft.Extensions.DI)
```csharp
// ITransportPublisher keyed by publisher name
sp.GetRequiredKeyedService<ITransportPublisher>("rabbitmq");

// IDeliveryIntent<TContext> keyed by intent name
sp.GetRequiredKeyedService<IDeliveryIntent<TContext>>("send-pending");

// IRabbitMQPersistentConnection keyed by connection name
sp.GetKeyedService<IRabbitMQPersistentConnection>("rabbitmq");
```

### OutboxProxy (DispatchProxy pattern)
When a handler needs a domain-specific `IOutboxService` (typed interface), use the proxy:
```csharp
// Define typed interface
public interface IOrdersOutboxService : IOutboxService { }

// Register
services.AddOutboxProxy<IOrdersOutboxService, OrdersDbContext>();

// Inject in handler
public class CreateOrderHandler(IOrdersOutboxService outbox)
```
`OutboxProxy<T>` uses `DispatchProxy` to delegate to `IOutboxService<TContext>`.

---

## Pipeline Behavior Order (IPipelineBehavior.Order)
Behaviors are sorted ascending by `Order`. Lower = runs earlier (outer wrapper).
```
IdempotencyRequestBehavior    Order = int.MinValue       (outermost — prevent duplicate commands)
TransactionBehavior           Order = int.MaxValue - 20  (late — wraps DB transaction + outbox)
OperationExceptionBehavior    Order = int.MaxValue - 10  (innermost — exception → IOperationResult)
```
Execution nesting: Idempotency → ... → Transaction → OperationException → Handler

---

## MediatR Registration Helper
Juice exposes `MediatorServiceCollectionExtensions.AddMediatR()` that wraps
`IServiceCollection` and returns `MediatorBuilder` — not the original MediatR builder.
The actual mediator registered is `Juice.MediatR.Internal.Mediator`, NOT `MediatR.Mediator`.

Handler discovery: `cfg.RegisterServicesFromAssembly(assembly, scanNotifications: bool)`
- Without scanNotifications: registers `IRequestHandler<,>` + `IPipelineBehavior<,>`
- With scanNotifications: also registers `INotificationHandler<>`

---

## Extension Method Locations
| Extension | Namespace | File |
|-----------|-----------|------|
| `AddMessaging()` | `Microsoft.Extensions.DependencyInjection` | MessagingServiceCollectionExtensions |
| `AddOutbox()` | `Microsoft.Extensions.DependencyInjection` | OutboxMessagingBuilderExtensions |
| `AddDelivery()` | `Microsoft.Extensions.DependencyInjection` | DeliveryOutboxBuilderExtensions |
| `AddEventBus()` | `Microsoft.Extensions.DependencyInjection` | EventBusServiceCollectionExtensions |
| `AddRabbitMQ()` | on EventBusBuilder | RabbitMQServiceCollectionExtensions |
| `AddMediatR()` | `Microsoft.Extensions.DependencyInjection` | MediatorServiceCollectionExtensions |
| `AddUnitOfWork<T,TContext>()` | `Microsoft.Extensions.DependencyInjection` | UnitOfWorkServiceCollectionExtensions |
| `AddOutboxProxy<T,TC>()` | `Microsoft.Extensions.DependencyInjection` | MessagingServiceCollectionExtensions |

---

## UnitOfWork Registration
```csharp
services.AddUnitOfWork<TEntity, TContext>();
// Registers IRepository<TEntity> backed by RepositoryBase<TEntity, TContext>
// Assumes TContext : DbContext, IUnitOfWork
```

---

## MessageContext (AsyncLocal)
Initialized at entry points (HTTP middleware or consumer engine), cleared on exit.
```csharp
// HTTP: MessageContextMiddleware
// Consumer: RabbitMQConsumerEngine.ProcessingEventAsync
// Tests: [InitializeMessageContext] attribute or MessageContext.InitializeTestContext()
MessageContext.Initialize(correlationId, causationId, executionId, source);
// Access anywhere in call chain:
var ctx = MessageContext.Current; // throws if not initialized
```

---

## EventBus Consumer DI Details
```csharp
services.AddEventBus(builder => {
    builder.AddPublishingServices();           // IEventBus → CompositeEventPublisher (singleton)
    builder.AddConsumerServices(key);          // ISubscriptionsManager (keyed singleton)
    builder.AddConsumerRetryPolicies(...);     // ConsumeRetryPolicyOptions
});
```
### Consumer Service Lifetimes
| Service | Lifetime |
|---------|----------|
| `IEventBus` (CompositeEventPublisher) | Singleton |
| `ISubscriptionsManager` | Keyed Singleton per consumer |
| `ITransportPublisher` | Keyed Singleton per producer |
| `IRabbitMQPersistentConnection` | Keyed Singleton per connection |
| `IntegrationEventDispatcher` | Transient (per dispatch) |
| `IIntegrationEventHandler<>` | Transient (per dispatch) |
| `RabbitMQConsumerHostedService` | Singleton (IHostedService) |

### Consumer Pipeline
```
RabbitMQConsumerEngine receives message
  → Extract headers (AMQP encoding) + deserialize to IIntegrationEvent
  → Initialize MessageContext (correlationId, causationId=messageId, executionId, source)
  → Resolve tenant via IScopedTenantResolver (from x-tenant-id header)
  → IntegrationEventDispatcher:
      → IIdempotencyService check (key: "{EventName}:{Source}:{MessageId}")
      → Resolve handler(s) from ISubscriptionsManager
      → Execute handler(s) in new DI scope
  → Result: Success/Duplicated → BasicAck | Failure → Retry/DLQ
```

### Retry Topology (RabbitMQ)
```
Main exchange (direct) → main queue
  on failure → retry exchange (topic)
    → retry.10s queue (TTL, DLX→main)
    → retry.1m queue (TTL, DLX→main)
    → retry.5m queue (TTL, DLX→main)
  on max retries → parking queue (no consumer)
```
Configured via `ConsumeRetryPolicyOptions` → `RetryPolicyProvider`.

---

## Juice.Measurement — Time Tracking
```csharp
services.AddExecutionTimeMeasurement();  // scoped ITimeTracker
// Usage:
using (tracker.BeginScope("Operation")) {
    tracker.Checkpoint("Step 1");
}
Console.WriteLine(tracker.ToString(humanReadable: true));
```
Scoped service; nested scopes + checkpoints; renders as aligned table.
Injected into `DbContextBase` when `DbOptions.EnableTimeTracking = true`.

---

## Utility Classes (Juice project)
- `StringIdGenerator.Instance` — Crockford Base32 unique IDs from GUIDs; `GenerateRandomId(length)`
- `QueryableExtensions` — `OrderBy<T>(propertyName)` etc. — dynamic LINQ sorting via expression trees
- `DictionaryExtensions` — `GetOption<T>` (deep dot/bracket access), `MergeOptions`, `Set` (dot-notation)
- `EnumExtensions` — `DisplayValue()` (`[Display]`), `StringValue()` (`[EnumMember]`)
