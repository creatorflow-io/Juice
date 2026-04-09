# Data Model: Consumer Builder Interface

**Feature**: 001-consumer-builder-interface
**Date**: 2026-04-09

No persistent data model changes — this feature introduces a pure C# interface with no storage, migrations, or schema changes.

---

## Type Model

### New: `IConsumerBuilder` (interface)

**Assembly**: `Juice.EventBus`
**Namespace**: `Juice.EventBus` (same namespace as `ISubscriptionsProvider`)
**File**: `core/src/Juice.EventBus/IConsumerBuilder.cs`

```
IConsumerBuilder
└── Subscribe<TEvent, THandler>(string? route = default) : IConsumerBuilder
    where TEvent : IIntegrationEvent
    where THandler : class, IIntegrationEventHandler<TEvent>
```

**Side effects of `Subscribe` (documented, not enforced by the interface)**:
- Registers `THandler` as a transient DI service via `IServiceCollection.TryAddTransient<THandler>()`.
- Accumulates a subscription record for use by the transport-specific subscriptions manager.

---

### Modified: `RabbitMQConsumerBuilder` (existing)

**Location**: `core/src/Juice.EventBus.RabbitMQ/Consuming/RabbitMQConsumerBuilder.cs`

Added: `: IConsumerBuilder` in implements list.
Added: explicit interface implementation delegating to the existing concrete method.

Existing public API unchanged:
```
RabbitMQConsumerBuilder.Subscribe<TEvent, THandler>(string? route = default) : RabbitMQConsumerBuilder
```

New (explicit interface impl, not visible on concrete type):
```
IConsumerBuilder.Subscribe<TEvent, THandler>(string? route) : IConsumerBuilder
```

---

### Modified: `LocalConsumerBuilder` (existing)

**Location**: `core/src/Juice.Messaging.Local/Internal/LocalConsumerBuilder.cs`

Added: `: IConsumerBuilder` in implements list.
Added: explicit interface implementation delegating to the existing concrete method.

Existing public API unchanged:
```
LocalConsumerBuilder.Subscribe<TEvent, THandler>(string? key = null) : LocalConsumerBuilder
```

New (explicit interface impl, not visible on concrete type):
```
IConsumerBuilder.Subscribe<TEvent, THandler>(string? route) : IConsumerBuilder
```

---

## Dependency Graph (unchanged)

```
Juice.Messaging.Contracts
    └── Juice.EventBus          ← IConsumerBuilder lives here (NEW)
            ├── Juice.EventBus.RabbitMQ   ← RabbitMQConsumerBuilder implements IConsumerBuilder
            └── Juice.Messaging.Local     ← LocalConsumerBuilder implements IConsumerBuilder
```

No new edges added.
