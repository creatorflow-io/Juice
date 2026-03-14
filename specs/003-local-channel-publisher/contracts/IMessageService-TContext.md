# Contract: IMessageService\<TContext\>

**Project**: `Juice.Messaging`
**Namespace**: `Juice.Messaging`
**Layer**: Messaging (application-layer abstraction)

---

## Interface Definition

```csharp
namespace Juice.Messaging
{
    /// <summary>
    /// Unified application-layer publishing interface for all message routes.
    /// Extends IMessageService with outbox-backed delivery for "local" and broker routes.
    /// TContext identifies which DbContext owns the outbox for this service.
    /// </summary>
    public interface IMessageService<TContext> : IMessageService
        where TContext : class
    {
        // Inherits: Task PublishAsync(IMessage message, CancellationToken cancellationToken = default);
        // Same method; implementation handles all three route types.
    }
}
```

---

## Behavior Contract

| Route resolved | Behavior |
|---|---|
| `"local-channel"` | Enqueued to `Channel<IMessage>`. Returns immediately. Non-durable. |
| `"local"` | Written to outbox (`OutboxDelivery` with `PublisherKey = "local"`). After outbox commit, also enqueued to `Channel<IMessage>` for immediate best-effort dispatch. `IntegrationEventDispatcher` idempotency deduplicates when `DeliveryHostedService` later processes the same outbox record via `LocalTransportPublisher`. Durable, retryable. |
| Broker (e.g. `"rabbitmq"`) | Written to outbox identically to existing flow. Delivered by existing `DeliveryHostedService` → `RabbitMQProducer`. |

---

## Transaction Behavior

| Context | Action |
|---|---|
| Inside `TransactionBehavior` scope (`TContext.Database.CurrentTransaction != null`) | `IOutboxService<TContext>.AddEventAsync(msg)` only. `TransactionBehavior` commits via `SaveEventsAsync`. No immediate channel dispatch (transaction not yet committed). |
| Outside active transaction | `AddEventAsync(msg)` + `SaveEventsAsync(null, ct)` immediately. Then, for `"local"` routes, enqueue to channel for immediate dispatch (idempotency deduplicates with delivery retry). |

---

## Registration

```csharp
services.AddMessaging()
    .AddMessageService<MyDbContext>();
// implies AddLocalChannel() and registers IMessageService<MyDbContext> (scoped)
// AddLocalPublisher() must also be called to register LocalTransportPublisher keyed "local"
```

---

## Constraints

- Must be registered as **Scoped** (shares `IOutboxService<TContext>` and `TContext` within the request scope).
- The `"local"` publisher key must be registered via `AddLocalPublisher()` for outbox delivery to work.
- `MessageContext` must be initialized at the call site.
- `TContext` must be the same context type used by `IOutboxService<TContext>` in the same DI scope.
