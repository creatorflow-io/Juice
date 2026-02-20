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
OperationExceptionBehavior    Order = 0        (outermost — catches all exceptions)
IdempotencyRequestBehavior    Order = 10       (before transaction)
TransactionBehavior           Order = int.MaxValue - 20  (innermost — wraps DB transaction)
```

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
