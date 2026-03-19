# API Contracts: DefaultOutboxContext

**Feature**: 006-default-outbox-context | **Date**: 2026-03-18

---

## New Public Types

### `DefaultOutboxContext` — `Juice.Messaging.Outbox.EF`

```csharp
/// <summary>
/// Standalone outbox DbContext for use outside domain transactions.
/// Implements IOutboxContext with the same table schema as OutboxContext.
/// IsManaged is always false — SaveEventsAsync executes immediately.
/// Tables must be created by running OutboxContext migrations.
/// </summary>
public class DefaultOutboxContext : DbContext, IOutboxContext
{
    public DbSet<OutboxEvent> Outbox { get; set; }
    public DbSet<OutboxDelivery> OutboxDeliveries { get; set; }

    public DefaultOutboxContext(DbContextOptions<DefaultOutboxContext> options);

    protected override void OnModelCreating(ModelBuilder modelBuilder);
}
```

---

## New Extension Methods

### `AddDefaultMessageService(configure)` — `Juice.Messaging.Outbox.EF`

```csharp
namespace Microsoft.Extensions.DependencyInjection;

public static class DefaultMessageServiceExtensions
{
    /// <summary>
    /// Registers DefaultOutboxContext and backs IMessageService with
    /// MessageService&lt;DefaultOutboxContext&gt;, enabling full-route publishing
    /// (local-channel, local, broker) outside domain transactions.
    ///
    /// Tables must exist — run OutboxContext migrations on the target database.
    /// Delivery is NOT auto-configured; call AddDelivery() separately or use the
    /// overload with autoWireDelivery in Juice.Messaging.Outbox.Delivery.
    /// </summary>
    public static MessagingBuilder AddDefaultMessageService(
        this MessagingBuilder builder,
        Action<DbContextOptionsBuilder> configure);
}
```

**Registers**:
- `DbContext<DefaultOutboxContext>` (scoped) — with caller-provided options
- `IOutboxRepository<>` (scoped, open generic) — via `builder.AddOutbox()`
- `IOutboxService<DefaultOutboxContext>` → `OutboxEventService<DefaultOutboxContext>` (scoped)
- `IPostCommitActions` → `PostCommitActions` (scoped, `TryAdd`)
- `IMessageService` → `MessageService<DefaultOutboxContext>` (scoped, `TryAdd`)

---

### `AddDefaultMessageService(configure, autoWireDelivery)` — `Juice.Messaging.Outbox.Delivery`

```csharp
namespace Microsoft.Extensions.DependencyInjection;

public static class DefaultMessageServiceDeliveryExtensions
{
    /// <summary>
    /// Registers DefaultOutboxContext and backs IMessageService with
    /// MessageService&lt;DefaultOutboxContext&gt;. When autoWireDelivery is true,
    /// also registers DeliveryHostedService&lt;DefaultOutboxContext&gt; for the
    /// "local" publisher key using framework default intents.
    /// </summary>
    /// <param name="configure">DbContext options (connection string, provider).</param>
    /// <param name="autoWireDelivery">
    ///   When true, auto-registers DeliveryHostedService&lt;DefaultOutboxContext&gt;
    ///   for the "local" publisher with default intents (send-pending, retry-failed,
    ///   recover-timeout). Default: false.
    /// </param>
    public static MessagingBuilder AddDefaultMessageService(
        this MessagingBuilder builder,
        Action<DbContextOptionsBuilder> configure,
        bool autoWireDelivery = false);
}
```

**Registers** (in addition to the EF overload):
- When `autoWireDelivery: true`: `DeliveryHostedService<DefaultOutboxContext>` (hosted, `"local"` publisher, all default intents)

---

## Usage Examples

### Minimal — local-channel only (no outbox DB needed)

```csharp
services.AddMessaging()
    .AddLocalChannel()
    .AddMessageService(); // legacy: local-channel only
```

### Full-route without delivery auto-wire (EF package only)

```csharp
services.AddMessaging()
    .AddLocalChannel()
    .AddPublishingPolicies(config.GetSection("Juice:Publishing"))
    .AddDefaultMessageService(opts =>
        opts.UseSqlServer(config.GetConnectionString("DefaultOutbox")));

// Delivery configured separately:
services.AddMessaging()
    .AddDelivery(d => d.AddDeliveryProcessor<DefaultOutboxContext>("local"));
```

### Full-route with auto-wire delivery (Delivery package)

```csharp
services.AddMessaging()
    .AddLocalChannel()
    .AddPublishingPolicies(config.GetSection("Juice:Publishing"))
    .AddDefaultMessageService(
        configure: opts => opts.UseSqlServer(config.GetConnectionString("DefaultOutbox")),
        autoWireDelivery: true);
```

### Coexistence with domain-aware IMessageService\<TContext\>

```csharp
services.AddMessaging()
    .AddLocalChannel()
    .AddPublishingPolicies(config.GetSection("Juice:Publishing"))
    // Domain-aware: used inside TransactionBehavior command handlers
    .AddMessageService<AppDbContext>()
    // Default outbox: used in controllers, background services, etc.
    .AddDefaultMessageService(
        configure: opts => opts.UseSqlServer(config.GetConnectionString("DefaultOutbox")),
        autoWireDelivery: true);
```

---

## Behaviour Contract

| Condition | Behaviour |
|-----------|-----------|
| Policy resolves `"local-channel"` | Message enqueued to in-memory channel; no DB write |
| Policy resolves `"local"` | Message written to `DefaultOutboxContext` outbox; `SaveEventsAsync(null)` called immediately |
| Policy resolves broker key | Message written to `DefaultOutboxContext` outbox; delivery by `DeliveryHostedService<DefaultOutboxContext>` |
| `MessageContext` not initialized | `InvalidOperationException` thrown for outbox routes (same as `MessageService<TContext>`) |
| DB unavailable | Exception propagates to caller; no silent data loss |
| `AddLocalChannel()` not called | `"local-channel"` dispatch logs warning and no-ops; no exception |
| Both `AddMessageService()` and `AddDefaultMessageService()` called | First `TryAddScoped` wins; second is no-op; document as misconfiguration |
