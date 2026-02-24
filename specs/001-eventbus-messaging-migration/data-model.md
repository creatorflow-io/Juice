# Data Model: EventBus Contracts → Messaging Contracts Migration

**Branch**: `001-eventbus-messaging-migration` | **Date**: 2026-02-24

## Type Hierarchy: Before vs After

### Before (current state)

```
Juice.Contracts (assembly: Juice.Contracts)
├── Juice.IMessage                        { MessageId, CreatedAt, TenantId }
├── Juice.IEvent : IMessage               { EventName }
└── Juice.MessageBase : IMessage          (abstract record)

Juice.EventBus.Contracts (assembly: Juice.EventBus.Contracts)  ← CANONICAL
├── Juice.EventBus.IIntegrationEvent : Juice.IEvent
├── Juice.EventBus.IntegrationEvent : MessageBase, IIntegrationEvent  (abstract record)
│       └── EventName → GetType().Name
└── Juice.EventBus.IIntegrationEventHandler<in T> where T : IIntegrationEvent
        └── Task HandleAsync(T @event)
```

### After (target state)

```
Juice.Contracts (assembly: Juice.Contracts)  ← UNCHANGED
├── Juice.IMessage
├── Juice.IEvent : IMessage
└── Juice.MessageBase : IMessage

Juice.Messaging.Contracts (assembly: Juice.Messaging.Contracts)  ← NEW CANONICAL
├── Juice.Messaging.IIntegrationEvent : Juice.IEvent
├── Juice.Messaging.IntegrationEvent : MessageBase, IIntegrationEvent  (abstract record)
│       └── EventName → GetType().Name
└── Juice.Messaging.IIntegrationEventHandler<in T> where T : Juice.Messaging.IIntegrationEvent
        └── Task HandleAsync(T @event)

Juice.EventBus.Contracts (assembly: Juice.EventBus.Contracts)  ← DEPRECATED SHIM
├── Juice.EventBus.IIntegrationEvent : Juice.Messaging.IIntegrationEvent   ← extends canonical
│       [Obsolete("Use Juice.Messaging.IIntegrationEvent. Migrate to Juice.Messaging.Contracts.")]
├── Juice.EventBus.IntegrationEvent : Juice.Messaging.IntegrationEvent      ← extends canonical
│       [Obsolete("Use Juice.Messaging.IntegrationEvent. Migrate to Juice.Messaging.Contracts.")]
└── Juice.EventBus.IIntegrationEventHandler<in T>                           ← standalone (deprecated)
    where T : Juice.EventBus.IIntegrationEvent
        [Obsolete("Use Juice.Messaging.IIntegrationEventHandler<T>. Migrate to Juice.Messaging.Contracts.")]
        └── Task HandleAsync(T @event)
```

## Package Dependency Graph: After Migration

```
Juice.Contracts
      │
      ├──────────────────────────────────────┐
      │                                      │
Juice.Messaging.Contracts (NEW)    Juice.EventBus.Contracts (SHIM)
      │                                 references Juice.Messaging.Contracts
      │
      ├── Juice.EventBus               (swaps to Juice.Messaging.Contracts)
      │       └── Juice.EventBus.RabbitMQ
      │
      └── (transitively used by)
              Juice.EF.Tests.Shared    (swaps to Juice.Messaging.Contracts)
              Juice.EventBus.Tests     (via Juice.EventBus, no .csproj change)
              Juice.Integrations.Tests (via Juice.EventBus, no .csproj change)
```

## Files to Create

### `core/src/Juice.Messaging.Contracts/` (new project)

```
core/src/Juice.Messaging.Contracts/
├── Juice.Messaging.Contracts.csproj
├── IIntegrationEvent.cs
├── IntegrationEvent.cs
└── IIntegrationEventHandler.cs
```

## Files to Modify

### `core/src/Juice.EventBus.Contracts/` (deprecated shim)

| File | Change |
|------|--------|
| `Juice.EventBus.Contracts.csproj` | Add `ProjectReference` to `Juice.Messaging.Contracts`; update `<Description>` |
| `IIntegrationEvent.cs` | Change base from `IEvent` → `Juice.Messaging.IIntegrationEvent`; add `[Obsolete]` |
| `IntegrationEvent.cs` | Change base from `MessageBase, IIntegrationEvent` → `Juice.Messaging.IntegrationEvent`; add `[Obsolete]` |
| `IIntegrationEventHandler.cs` | Add `[Obsolete]`; constraint updated to `Juice.EventBus.IIntegrationEvent` (existing) |

### `core/src/Juice.EventBus/` (internal consumer)

| File | Change |
|------|--------|
| `Juice.EventBus.csproj` | Replace `Juice.EventBus.Contracts` reference with `Juice.Messaging.Contracts` |
| `Dispatching/IntegrationEventDispatcher.cs` | Update `typeof(IIntegrationEventHandler<>)` to use `Juice.Messaging.IIntegrationEventHandler<>` |
| `*.cs` (using statements) | Update `using Juice.EventBus` where referring to contract types → `using Juice.Messaging` |

### `core/test/Juice.EF.Tests.Shared/` (test shared — demonstrates canonical usage)

| File | Change |
|------|--------|
| `Juice.EF.Tests.Shared.csproj` | Replace `Juice.EventBus.Contracts` reference with `Juice.Messaging.Contracts` |
| `Events/ContentPublishedIntegrationEvent.cs` | Update `using Juice.EventBus` → `using Juice.Messaging`; base class unchanged |
| `Events/ContentNameChangedIntegrationEvent.cs` | Same update |

## Runtime Compatibility Matrix

| Scenario | Compile? | Runtime? | Note |
|----------|----------|----------|------|
| Old consumer uses `Juice.EventBus.IntegrationEvent` as base | ✅ | ✅ | Subtype of canonical |
| Old consumer uses `Juice.EventBus.IIntegrationEvent` in method signature | ✅ | ✅ | Subtype of canonical |
| New consumer uses `Juice.Messaging.IntegrationEvent` as base | ✅ | ✅ | Canonical |
| Dispatcher finds old handler via `Juice.Messaging.IIntegrationEventHandler<T>` resolution | ✅ | ✅ | Old `IIntegrationEventHandler<T>` extends canonical → satisfies `IsAssignableTo` |
| Old event instance passed to code expecting `Juice.Messaging.IIntegrationEvent` | ✅ | ✅ | Upcast via subtype |
| New event instance passed to code expecting `Juice.EventBus.IIntegrationEvent` | ✅ | ❌ | New types do NOT implement old shim interfaces (no downcast) |

> **Note on last row**: New events inheriting from `Juice.Messaging.IntegrationEvent` do NOT
> implement `Juice.EventBus.IIntegrationEvent`. Code that explicitly types variables as
> `Juice.EventBus.IIntegrationEvent` and receives a new-style event will fail at runtime.
> This is acceptable: new code should use `Juice.Messaging.IIntegrationEvent`; mixed-usage
> patterns (new event, old typed variable) require a migration step in the consuming code.
> The `IntegrationEventDispatcher` uses `Juice.Messaging.IIntegrationEventHandler<T>` after
> the migration, so dispatch itself always works regardless of which base the event uses.
