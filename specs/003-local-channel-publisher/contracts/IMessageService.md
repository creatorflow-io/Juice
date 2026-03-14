# Contract: IMessageService

**Project**: `Juice.Messaging`
**Namespace**: `Juice.Messaging`
**Layer**: Messaging (application-layer abstraction)

---

## Interface Definition

```csharp
namespace Juice.Messaging
{
    /// <summary>
    /// Unified application-layer publishing interface for in-process messages.
    /// Supports "local-channel" route (zero DB, non-durable).
    /// For outbox-backed routes ("local" or broker), use IMessageService<TContext>.
    /// </summary>
    public interface IMessageService
    {
        /// <summary>
        /// Publishes a message according to the resolved routing policy.
        /// - "local-channel" routes: enqueued to in-memory channel; returns before handler executes.
        /// - Other routes: not handled by the non-generic implementation; use IMessageService<TContext>.
        /// </summary>
        Task PublishAsync(IMessage message, CancellationToken cancellationToken = default);
    }
}
```

---

## Behavior Contract

| Route resolved | Behavior |
|---|---|
| `"local-channel"` | Enqueued to `Channel<IMessage>`. Method returns immediately. Handler runs asynchronously on `LocalChannelBackgroundService`. |
| Any other route | Not handled by non-generic implementation. Caller should use `IMessageService<TContext>`. |

---

## Registration

```csharp
services.AddMessaging()
    .AddLocalChannel();   // registers IMessageService (non-generic) + background service
```

---

## Constraints

- `IMessageService` (non-generic) does **not** write to the database.
- Events dispatched via `"local-channel"` are **non-durable** — lost on process restart.
- `MessageContext` must be initialized at the call site (same rule as all entry points).
