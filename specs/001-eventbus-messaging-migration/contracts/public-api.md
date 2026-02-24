# Public API Contract: `Juice.Messaging.Contracts`

**Branch**: `001-eventbus-messaging-migration` | **Date**: 2026-02-24
**Package**: `Juice.Messaging.Contracts`
**Namespace**: `Juice.Messaging`

---

## Interface: `IIntegrationEvent`

```csharp
namespace Juice.Messaging
{
    /// <summary>
    /// Marker interface for integration events published across service boundaries.
    /// Implement this interface (or inherit <see cref="IntegrationEvent"/>) to define
    /// an event that can be published via the Juice event bus.
    /// </summary>
    public interface IIntegrationEvent : IEvent   // IEvent from Juice.Contracts
    {
        // Inherits from IEvent:
        //   string EventName { get; }
        //
        // Inherits from IMessage (via IEvent):
        //   Guid MessageId { get; }
        //   DateTimeOffset CreatedAt { get; }
        //   string? TenantId { get; }
    }
}
```

**Constraints**:
- No additional members beyond what is inherited from `IEvent` / `IMessage`.
- Serves as the type constraint for `IIntegrationEventHandler<T>` and for
  event bus subscription registration.

---

## Abstract Record: `IntegrationEvent`

```csharp
namespace Juice.Messaging
{
    /// <summary>
    /// Base class for all integration events. Inherit from this record to define
    /// a concrete integration event with built-in message identity, timestamp, and
    /// tenant support.
    /// </summary>
    public abstract record IntegrationEvent : MessageBase, IIntegrationEvent
    {
        // Inherits from MessageBase:
        //   virtual Guid MessageId { get; init; }         = Guid.NewGuid()
        //   virtual DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow
        //   virtual string? TenantId { get; protected set; }

        /// <summary>
        /// The name used to identify this event type during routing and dispatch.
        /// Defaults to the concrete class name.
        /// </summary>
        public virtual string EventName => GetType().Name;
    }
}
```

**Usage pattern**:
```csharp
[Domain("Orders")]
public record OrderPlacedEvent : IntegrationEvent
{
    public Guid OrderId { get; init; }
    public decimal Total { get; init; }
}
```

---

## Interface: `IIntegrationEventHandler<T>`

```csharp
namespace Juice.Messaging
{
    /// <summary>
    /// Handler for a specific integration event type.
    /// Register implementations with the DI container via the event bus subscription builder.
    /// </summary>
    /// <typeparam name="TIntegrationEvent">
    /// The integration event type this handler processes.
    /// </typeparam>
    public interface IIntegrationEventHandler<in TIntegrationEvent>
        where TIntegrationEvent : IIntegrationEvent
    {
        /// <summary>
        /// Processes the received integration event.
        /// </summary>
        Task HandleAsync(TIntegrationEvent @event);
    }
}
```

**Usage pattern**:
```csharp
public class OrderPlacedHandler : IIntegrationEventHandler<OrderPlacedEvent>
{
    public async Task HandleAsync(OrderPlacedEvent @event)
    {
        // process the event
    }
}
```

---

## Package Metadata

**`Juice.Messaging.Contracts.csproj`** (reference):

| Property | Value |
|----------|-------|
| TargetFrameworks | `$(AppTargetFramework)` → `net6.0;net8.0;net9.0` |
| RootNamespace | `Juice.Messaging` |
| Description | Canonical contracts for Juice integration events. |
| Dependencies | `Juice.Contracts` (ProjectReference) |

---

## Deprecated Package: `Juice.EventBus.Contracts`

**`Juice.EventBus.Contracts`** (shim — updated package metadata):

| Property | Value |
|----------|-------|
| Description | **[Deprecated]** Use `Juice.Messaging.Contracts` instead. This package re-exports integration event contracts from `Juice.Messaging.Contracts` for backward compatibility. |
| Dependencies | `Juice.Messaging.Contracts` (ProjectReference, new) + `Juice.Contracts` (existing) |

**Re-exported types** (in `Juice.EventBus` namespace, all marked `[Obsolete]`):

| Type | Kind | Extends |
|------|------|---------|
| `IIntegrationEvent` | interface | `Juice.Messaging.IIntegrationEvent` |
| `IntegrationEvent` | abstract record | `Juice.Messaging.IntegrationEvent` |
| `IIntegrationEventHandler<T>` | interface | *(standalone, mirrors new interface)* |

---

## Migration Path for Consumers

### Step 1: Switch package reference
```xml
<!-- Before -->
<ProjectReference Include="...\Juice.EventBus.Contracts\Juice.EventBus.Contracts.csproj" />

<!-- After -->
<ProjectReference Include="...\Juice.Messaging.Contracts\Juice.Messaging.Contracts.csproj" />
```

### Step 2: Update using statements
```csharp
// Before
using Juice.EventBus;

// After
using Juice.Messaging;
```

### Step 3: No other changes needed
- `IntegrationEvent`, `IIntegrationEvent`, `IIntegrationEventHandler<T>` retain the same
  member signatures; no renaming, no new required members.
