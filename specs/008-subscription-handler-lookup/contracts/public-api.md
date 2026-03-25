# Public API Contracts: Subscriptions Manager Handler Lookup for Local Routes

**Feature**: 008-subscription-handler-lookup
**Date**: 2026-03-25
**Library**: `Juice.Messaging.Local`

---

## New Public Types

### `LocalConsumerBuilder`

**Namespace**: `Juice.Messaging`
**Assembly**: `Juice.Messaging.Local`

```csharp
public sealed class LocalConsumerBuilder
{
    // Registers THandler as a transient DI service and records the
    // TEvent → THandler mapping in the subscriptions manager.
    // key: optional override for the event routing key (defaults to TEvent.Name).
    public LocalConsumerBuilder Subscribe<TEvent, THandler>(string? key = null)
        where TEvent : IIntegrationEvent
        where THandler : class, IIntegrationEventHandler<TEvent>;
}
```

**Notes**:
- Mirrors `RabbitMQConsumerBuilder.Subscribe<TEvent, THandler>()` API.
- `Subscribe` is idempotent for the same `(TEvent, THandler, key)` triple.

---

## New Extension Methods

### `MessagingBuilder.AddLocalConsumer`

**Namespace**: `Juice.Messaging`
**Assembly**: `Juice.Messaging.Local`

```csharp
public static class LocalMessagingBuilderExtensions
{
    // Registers the keyed ISubscriptionsManager (key = "local") for in-process routes
    // and invokes configure to populate handler subscriptions.
    // Safe to call multiple times — subsequent calls add to the existing manager.
    public static MessagingBuilder AddLocalConsumer(
        this MessagingBuilder builder,
        Action<LocalConsumerBuilder> configure);
}
```

**Behavior**:
1. Creates a `LocalConsumerBuilder` backed by the current `IServiceCollection`.
2. Calls `configure(builder)` to accumulate `SubscriptionInfo` records.
3. Registers an `ISubscriptionsProvider` with the accumulated records.
4. Ensures a keyed-singleton `ISubscriptionsManager` is registered under key `"local"`.
5. When the host resolves `ISubscriptionsManager` (key `"local"`) for the first time, `InMemorySubscriptionsManager` reads all registered `ISubscriptionsProvider` instances and populates itself.

---

## Changed Behavior (No Interface Changes)

### `LocalDispatchHelper.DispatchIntegrationEventAsync` *(internal)*

```csharp
// BEFORE (current):
internal static Task<EventDispatchResult> DispatchIntegrationEventAsync(
    IServiceProvider serviceProvider,
    IntegrationEventDispatcher dispatcher,
    IIntegrationEvent evt,
    CancellationToken cancellationToken);

// AFTER:
internal static Task<EventDispatchResult> DispatchIntegrationEventAsync(
    IServiceProvider serviceProvider,
    IntegrationEventDispatcher dispatcher,
    ISubscriptionsManager? subscriptionsManager,   // NEW — nullable
    IIntegrationEvent evt,
    CancellationToken cancellationToken);
```

**Dispatch logic**:
- If `subscriptionsManager` is non-null: call `GetHandlersForEventAsync(evt.GetType().Name)` to get handler types.
- If `subscriptionsManager` is null: fall back to `serviceProvider.GetServices(IIntegrationEventHandler<T>)` (existing behavior, backward compatible).

### `LocalChannelBackgroundService` *(internal)*

- Injects `[FromKeyedServices("local")] ISubscriptionsManager? subscriptionsManager` (nullable — optional).
- Passes it to `LocalDispatchHelper.DispatchIntegrationEventAsync`.

### `LocalTransportPublisher` *(internal)*

- Injects `[FromKeyedServices("local")] ISubscriptionsManager? subscriptionsManager` (nullable — optional).
- Passes it to `LocalDispatchHelper.DispatchIntegrationEventAsync`.

---

## No Changes To

- `ISubscriptionsManager` interface — unchanged.
- `SubscriptionInfo`, `ISubscriptionsProvider`, `InMemorySubscriptionsManager` — unchanged.
- `IIntegrationEventHandler<T>` — unchanged.
- RabbitMQ consumer registration and dispatch paths — unchanged.
- `IntegrationEventDispatcher` — unchanged.
