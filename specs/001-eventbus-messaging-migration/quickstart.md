# Quickstart: EventBus Contracts → Messaging Contracts Migration

**Branch**: `001-eventbus-messaging-migration` | **Date**: 2026-02-24

---

## Overview

This guide covers two audiences:

1. **Existing consumers** — your existing code continues to compile without changes after
   upgrading `Juice.EventBus.Contracts`. This guide shows how to optionally migrate to the
   new canonical package.

2. **New consumers** — start here with `Juice.Messaging.Contracts`.

---

## For Existing Consumers: Zero-Change Upgrade

After upgrading `Juice.EventBus.Contracts` to version 9.x, your existing code compiles with
**deprecation warnings only** (CS0618). No logic changes are required.

```csharp
// This code continues to compile and work at runtime with no changes
using Juice.EventBus;                           // ← deprecation warning CS0618

[Domain("Orders")]
public record OrderPlacedEvent : IntegrationEvent   // ← IntegrationEvent from Juice.EventBus
{
    public Guid OrderId { get; init; }
}

public class OrderPlacedHandler : IIntegrationEventHandler<OrderPlacedEvent>
{
    public async Task HandleAsync(OrderPlacedEvent @event) { ... }
}
```

To suppress warnings temporarily while planning migration:
```csharp
#pragma warning disable CS0618
using Juice.EventBus;
#pragma warning restore CS0618
```

---

## For Existing Consumers: Optional Migration to New Namespace

### Step 1 — Update .csproj

```xml
<!-- Remove -->
<ProjectReference Include="...\Juice.EventBus.Contracts\Juice.EventBus.Contracts.csproj" />

<!-- Add -->
<ProjectReference Include="...\Juice.Messaging.Contracts\Juice.Messaging.Contracts.csproj" />
```

Or if consuming as NuGet packages:
```xml
<!-- Remove -->
<PackageReference Include="Juice.EventBus.Contracts" Version="9.*" />

<!-- Add -->
<PackageReference Include="Juice.Messaging.Contracts" Version="9.*" />
```

### Step 2 — Update using statements (all files)

```csharp
// Before
using Juice.EventBus;

// After
using Juice.Messaging;
```

That is the **only required code change**. No type renames, no member changes.

### Step 3 — Verify

Build and run your existing tests. All event definitions, handler implementations, and
DI registrations continue to work as before.

---

## For New Consumers: Starting Fresh

Reference `Juice.Messaging.Contracts` in your project:

```xml
<ItemGroup>
  <ProjectReference Include="...\Juice.Messaging.Contracts\Juice.Messaging.Contracts.csproj" />
</ItemGroup>
```

### Defining an integration event

```csharp
using Juice.Messaging;
using Juice.Messaging.Attributes;   // for [Domain]

[Domain("Inventory")]           // controls outbox routing; value = domain name
public record StockDepletedEvent : IntegrationEvent
{
    public string ProductId { get; init; } = default!;
    public int RemainingQuantity { get; init; }
}
```

### Implementing a handler

```csharp
using Juice.Messaging;

public class StockDepletedHandler : IIntegrationEventHandler<StockDepletedEvent>
{
    private readonly ILogger<StockDepletedHandler> _logger;

    public StockDepletedHandler(ILogger<StockDepletedHandler> logger)
    {
        _logger = logger;
    }

    public async Task HandleAsync(StockDepletedEvent @event)
    {
        _logger.LogInformation("Stock depleted for product {ProductId}", @event.ProductId);
        // handle the event
        await Task.CompletedTask;
    }
}
```

### Registering the handler (via event bus subscription builder)

Handler registration is performed through the `Juice.EventBus` builder — no change to that
registration API is required after this migration.

---

## Validation Checklist

After migrating a consumer project:

- [ ] Project builds with zero errors
- [ ] No unresolved `Juice.EventBus` references remain in migrated files
- [ ] Existing integration tests pass without modification
- [ ] New event type compiles and can be dispatched end-to-end
- [ ] Handler receives events published from both old-style and new-style producers
