# Data Model: Subscriptions Manager Handler Lookup for Local Routes

**Feature**: 008-subscription-handler-lookup
**Date**: 2026-03-25

> This feature does not introduce new persistent entities (no database tables, no migrations).
> All data is held in-memory within the `ISubscriptionsManager` singleton for the lifetime of the host process.

---

## Runtime Entities

### SubscriptionInfo *(existing — no changes)*

Represents a single (event type → handler type) binding, keyed by a routing key string.

| Field        | Type     | Notes                                                     |
|--------------|----------|-----------------------------------------------------------|
| `EventType`  | `Type`   | The CLR type implementing `IIntegrationEvent`             |
| `HandlerType`| `Type`   | The CLR type implementing `IIntegrationEventHandler<T>`   |
| `Key`        | `string` | Routing/event-name key; defaults to `EventType.Name`     |
| `IsDynamic`  | `bool`   | Always `false` for local-route subscriptions              |

**Used by**: `ISubscriptionsManager.AddSubscription`, `ISubscriptionsManager.GetHandlersForEventAsync`

---

### ISubscriptionsManager — local instance *(keyed "local")*

A singleton `InMemorySubscriptionsManager` registered under DI key `"local"`. Populated at construction time from `ISubscriptionsProvider` instances gathered by `LocalConsumerBuilder`.

| Attribute            | Value                                       |
|----------------------|---------------------------------------------|
| DI lifetime          | Singleton                                   |
| DI key               | `"local"`                                   |
| topic support        | `false` (exact-name match only)             |
| Populated by         | `LocalConsumerBuilder` → `ISubscriptionsProvider` |
| Queried by           | `LocalDispatchHelper` (when non-null)       |

---

### LocalConsumerBuilder *(new)*

A transient builder object created during DI setup (i.e., within `AddLocalConsumer(...)`). Accumulates `SubscriptionInfo` records and registers handler types in `IServiceCollection`.

| Field          | Type                       | Notes                                           |
|----------------|----------------------------|-------------------------------------------------|
| `_services`    | `IServiceCollection`       | The host service collection                     |
| `_subscriptions`| `List<SubscriptionInfo>` | Accumulated registrations                        |

**Methods**:
- `Subscribe<TEvent, THandler>(string? key = null)` — adds `SubscriptionInfo` + `services.TryAddTransient<THandler>()`
- `Build()` → `ISubscriptionsProvider` — produces the provider that feeds `InMemorySubscriptionsManager`

---

## State Transitions (dispatch flow)

```
AddLocalConsumer(builder => builder.Subscribe<TEvent, THandler>())
    │
    ▼
LocalConsumerBuilder.Build() → InMemorySubscriptionsProvider
    │
    ▼
InMemorySubscriptionsManager("local") ← constructed at first DI resolve
    │
    ▼  (at dispatch time)
LocalDispatchHelper.DispatchIntegrationEventAsync(sp, dispatcher, subsManager, evt, ct)
    │
    ├── subsManager != null → subsManager.GetHandlersForEventAsync(evt.GetType().Name)
    │                             → List<Type> handlerTypes
    │
    └── subsManager == null → serviceProvider.GetServices(IIntegrationEventHandler<T>)
                                  → List<Type> handlerTypes (DI scan fallback)
    │
    ▼
IntegrationEventDispatcher.DispatchAsync(evt, EventDispatchContext(handlerTypes, ...))
    │
    ▼
Handler invoked per handlerType (resolved from scoped DI within dispatcher)
```
