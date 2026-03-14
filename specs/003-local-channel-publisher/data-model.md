# Data Model: Local Transport Publishers with Unified Message Service

**Date**: 2026-03-14 | **Branch**: `003-local-channel-publisher`

---

## No New Database Entities

This feature introduces **zero new DB tables or EF migrations**. It reuses the existing `OutboxEvents` and `OutboxDeliveries` tables for the `"local"` route. The `"local-channel"` route involves no persistence.

---

## New In-Memory Data Structures

### `Channel<IMessage>` — Local Channel Queue

| Property | Value |
|---|---|
| Type | `System.Threading.Channels.Channel<IMessage>` |
| Capacity | Unbounded |
| Lifetime | Singleton (shared between `IMessageService` writer and `LocalChannelBackgroundService` reader) |
| Durability | None — items lost on process shutdown |
| Item type | `IMessage` (concrete runtime type preserved; may be `INotification` or `IIntegrationEvent`) |

---

## Existing Entities Used (no change)

### `OutboxDelivery` — `"local"` route rows

When a message is routed to `"local"`, an `OutboxDelivery` row is written identically to broker routes. The only difference is `PublisherKey = "local"`. The existing partial index on `State = NotPublished` picks up these rows for the `SendPendingIntent`.

| Field | Value for "local" route |
|---|---|
| `PublisherKey` | `"local"` (reserved key) |
| `Destination` | Empty string or configured value (unused by `LocalTransportPublisher`) |
| `RoutingKey` | Null (unused for local dispatch) |
| `State` | `NotPublished` → `InProgress` → `Published` / `Failed` (same lifecycle) |
| `RetryCount` | Incremented on failure; subject to delivery policy |

---

## New Service Contracts (runtime types)

### `IMessageService` (non-generic)

- **Lifetime**: Scoped
- **State**: Stateless — holds only `IMessagePublishingPolicy`, `ChannelWriter<IMessage>`
- **Input**: `IMessage` (any concrete subtype: `INotification` or `IIntegrationEvent`)
- **Output**: `Task` (completes after channel enqueue; does NOT wait for handler)

### `IMessageService<TContext>` (generic)

- **Lifetime**: Scoped
- **State**: Holds `IOutboxService<TContext>`, `TContext` (for transaction detection), `ChannelWriter<IMessage>`
- **Input**: `IMessage`
- **Transaction branch**: determined at runtime by `TContext.Database.CurrentTransaction != null`

### `LocalChannelOptions`

| Property | Type | Default | Description |
|---|---|---|---|
| `MaxConcurrency` | `int?` | `null` (unlimited) | Maximum number of handlers executing simultaneously. `null` = no limit. `N` = at most N concurrent handlers via `SemaphoreSlim(N, N)`. |

### `LocalChannelBackgroundService`

- **Lifetime**: Singleton (IHostedService)
- **State**: `ChannelReader<IMessage>` reference + `SemaphoreSlim` (when `MaxConcurrency` is set)
- **Concurrency**: Single reader loop; each message dispatched as a new `Task.Run` (concurrent). Semaphore limits in-flight count when configured.
- **Per-message scope**: Creates a new `IServiceScope` per message (matches `DeliveryHostedService` pattern)
- **Dispatch**: Branches by runtime type of `IMessage`
- **Shutdown**: `CancellationToken` stops the reader loop; in-flight handler tasks complete with `CancellationToken.None`

---

## Dispatch Type Resolution

```
IMessage instance (runtime)
  │
  ├─ is INotification → INotificationPublisher.Publish<T>()   [via reflected MethodInfo cache]
  │
  └─ is IIntegrationEvent → IntegrationEventDispatcher.DispatchAsync()
       ├─ IIdempotencyService.TryCreateRequest(EventName, "{Source}:{MessageId}")
       │    → Succeeded: resolve and invoke handlers
       │    → Failed: return EventDispatchResult.Duplicated (skip)
       └─ Handler resolution: try concrete type first (RabbitMQ subscriptions),
            fall back to GetServices(IIntegrationEventHandler<T>) filtered by type (local-channel)
```

Both paths run inside a fresh DI scope to ensure handler lifetime isolation.

## Idempotency Key Structure

For `"local"` route messages dispatched both immediately (via channel) and via delivery retry:

| Component | Source (immediate dispatch) | Source (delivery retry via LocalTransportPublisher) |
|---|---|---|
| `EventName` | `evt.GetType().Name` | Same (deserialized from outbox payload) |
| `Source` | `MessageContext.Current.Source` | Restored from outbox header `x-source` |
| `MessageId` | `evt.MessageId` | Same (deserialized from outbox payload) |
| **Full key** | `"{EventName}:{Source}:{MessageId}"` | Same — enables cross-path deduplication |

The `IIdempotencyService` must be registered with a lifetime that allows state sharing across DI scopes (e.g., singleton for `InMemoryIdempotencyService`, or an external store like Redis/SQL for production).
