# Quickstart: DefaultOutboxContext

**Feature**: 006-default-outbox-context | **Date**: 2026-03-18

---

## Prerequisites

1. EF Core migrations for `OutboxContext` have been run against the target database.
2. A publishing policy is configured (`AddPublishingPolicies()`).
3. `MessageContext` is initialized at your entry point (middleware or `[InitializeMessageContext]` in tests).

---

## Package references

For full-route publishing **without** delivery auto-wire:
```xml
<PackageReference Include="Juice.Messaging.Outbox.EF" />
```

For full-route publishing **with** `autoWireDelivery: true`:
```xml
<PackageReference Include="Juice.Messaging.Outbox.Delivery" />
```
*(Delivery transitively references EF — one package covers both.)*

---

## Setup (3–5 lines)

```csharp
// Program.cs / Startup.cs
services.AddMessaging()
    .AddLocalChannel()                                          // in-process channel
    .AddPublishingPolicies(config.GetSection("Juice:Publishing"))
    .AddDefaultMessageService(
        configure: opts => opts.UseSqlServer(
            config.GetConnectionString("DefaultOutbox")),       // separate DB
        autoWireDelivery: true);                                // drain "local" outbox automatically
```

**appsettings.json** — connection string for `DefaultOutboxContext`:
```json
{
  "ConnectionStrings": {
    "DefaultOutbox": "Server=...;Database=outbox;..."
  }
}
```

---

## Publishing a message

```csharp
// Any controller, background service, or integration adapter:
public class OrderController(IMessageService messages) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> PlaceOrderAsync(PlaceOrderRequest req)
    {
        var evt = new OrderPlacedEvent(req.OrderId);
        await messages.PublishAsync(evt);          // routes per IMessagePublishingPolicy
        return Accepted();
    }
}
```

No `TContext` generic parameter required — `IMessageService` handles all routes.

---

## Coexistence with domain command handlers

```csharp
services.AddMessaging()
    .AddLocalChannel()
    .AddPublishingPolicies(config.GetSection("Juice:Publishing"))
    // Domain-aware (inside TransactionBehavior — deferred, atomic with domain data):
    .AddMessageService<AppDbContext>()
    // Default outbox (outside transactions — immediate write, separate DB):
    .AddDefaultMessageService(
        opts => opts.UseSqlServer(config.GetConnectionString("DefaultOutbox")),
        autoWireDelivery: true);
```

Inject the right service at each call site:

```csharp
// Command handler (inside TransactionBehavior):
public class CreateOrderHandler(IMessageService<AppDbContext> messages) { ... }

// Controller (outside transaction):
public class OrderController(IMessageService messages) { ... }
```

---

## Manual delivery configuration (when autoWireDelivery: false)

```csharp
services.AddMessaging()
    .AddDefaultMessageService(opts => opts.UseSqlServer(...))   // no autoWireDelivery
    .AddDelivery(d =>
        d.AddDeliveryProcessor<DefaultOutboxContext>("local")   // explicit, full control
         .AddDeliveryProcessor<DefaultOutboxContext>("rabbitmq"));
```

---

## Running migrations

`DefaultOutboxContext` uses the same tables as `OutboxContext`. Apply existing `OutboxContext` migrations to the target database:

```csharp
// Program.cs startup:
await app.Services.MigrateOutboxAsync<OutboxContext>();
// or run the migration SQL directly against the target DB
```

The `DefaultOutboxContext` tables are ready immediately after those migrations complete.
