# Data Model: OutboxDelivery Host/App Tracking Column

## Modified Entity: OutboxDelivery

**File**: `core/src/Juice.Messaging.Outbox/OutboxDelivery.cs`

### New Property

| Property | Type | Nullable | Max Length | Description |
|----------|------|----------|------------|-------------|
| `ProcessedBy` | `string?` | Yes | 128 (`NameLength`) | Identity of the host/app instance that last acted on this delivery |

### Value lifecycle

| Transition | Method | ProcessedBy written? |
|------------|--------|----------------------|
| `NotPublished` → `InProgress` | `MarkAsInProgressAsync` | Yes |
| `InProgress` → `Published` | `MarkAsPublishedAsync` | No (already set on InProgress) |
| `InProgress` / `Failed` → `Failed` | `MarkAsFailedAsync` | Yes (update on each retry) |
| Any → `Skipped` | `MarkAsSkippedAsync` | Yes |
| Record created | `SaveEventsAsync` | No (null until first attempt) |

---

## New Abstraction: IDeliveryNodeIdentity

**File**: `core/src/Juice.Messaging.Outbox/IDeliveryNodeIdentity.cs`

```
interface IDeliveryNodeIdentity
    string NodeId { get; }
```

- `NodeId` is computed once at application startup and remains constant for the process lifetime.
- Default format: `"{Environment.MachineName}:{Environment.ProcessId}"`
- Applications may register a custom `IDeliveryNodeIdentity` singleton to override the default.

**Default implementation**: `DeliveryNodeIdentity`  
**File**: `core/src/Juice.Messaging.Outbox.Delivery/Internal/DeliveryNodeIdentity.cs`  
**Registration**: singleton in `DeliveryBuilder` DI setup  

---

## EF Configuration Change

**File**: `core/src/Juice.Messaging.Outbox.EF/OutboxDeliveryEntityTypeConfiguration.cs`

Add inside `Configure()`:

```
builder.Property(e => e.ProcessedBy)
    .HasMaxLength(LengthConstants.NameLength);
```

No index required — this is a diagnostic/audit column, not a query filter column.

---

## Schema Migration

### SqlServer

**File**: `core/src/Juice.Messaging.Outbox.Migrations.SqlServer/[timestamp]_AddProcessedByToDelivery.cs`

```sql
-- Up
ALTER TABLE [<schema>].[OutboxDeliveries]
ADD [ProcessedBy] nvarchar(128) NULL;

-- Down
ALTER TABLE [<schema>].[OutboxDeliveries]
DROP COLUMN [ProcessedBy];
```

### PostgreSQL

**File**: `core/src/Juice.Messaging.Outbox.Migrations.PostgreSQL/[timestamp]_AddProcessedByToDelivery.cs`

```sql
-- Up
ALTER TABLE "OutboxDeliveries"
ADD "ProcessedBy" character varying(128);

-- Down
ALTER TABLE "OutboxDeliveries"
DROP COLUMN "ProcessedBy";
```

Both migrations follow the `ISchemaDbContext` injection pattern used in `AddRoutingKeyToDelivery`.

---

## Repository Change

**File**: `core/src/Juice.Messaging.Outbox.EF/OutboxRepository.cs`

`OutboxRepository<TContext>` constructor gains an optional `IDeliveryNodeIdentity? nodeIdentity` parameter (injected by DI; null-safe fallback to empty string for backward compatibility).

The three `ExecuteUpdateAsync` chains are extended:

```
// MarkAsInProgressAsync, MarkAsFailedAsync, MarkAsSkippedAsync — each adds:
.SetProperty(e => e.ProcessedBy, e => nodeIdentity != null ? nodeIdentity.NodeId : null)
```

`MarkAsPublishedAsync` is NOT changed — the node identity is already recorded when the delivery was claimed.
