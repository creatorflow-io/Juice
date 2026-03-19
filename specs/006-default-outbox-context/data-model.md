# Data Model: DefaultOutboxContext

**Feature**: 006-default-outbox-context | **Date**: 2026-03-18

---

## DefaultOutboxContext

**Location**: `core/src/Juice.Messaging.Outbox.EF/DefaultOutboxContext.cs`
**Layer**: EF (Infrastructure — DbContext)

```
DefaultOutboxContext
├── implements: DbContext, IOutboxContext
├── NOT IManagable → IsManaged is always false
├── NOT ISchemaDbContext (optional: add schema support if needed)
│
├── DbSet<OutboxEvent>      Outbox           ← table: OutboxEvents
└── DbSet<OutboxDelivery>   OutboxDeliveries ← table: OutboxDeliveries
```

**Constructor**:
```
DefaultOutboxContext(DbContextOptions<DefaultOutboxContext> options)
```

**OnModelCreating**: calls `this.ConfigureOutbox(modelBuilder)` — identical table/column/index configuration as `OutboxContext`. Schema is controlled by the optional schema parameter if `ISchemaDbContext` is implemented.

**DI lifetime**: Scoped (per request/operation scope)

---

## Entities (Inherited — No New Entities)

Both entities are defined in `Juice.Messaging.Outbox` and mapped by `OutboxEntityTypeConfiguration` / `OutboxDeliveryEntityTypeConfiguration` in `Juice.Messaging.Outbox.EF`. No changes to entity definitions.

### OutboxEvent

| Field | Type | Notes |
|-------|------|-------|
| EventId | Guid | PK |
| PayloadBytes | byte[] | Serialized message |
| Headers | Dictionary<string,object?> | JSON-stored; correlation-id, causation-id, tenant, message type, CLR type |
| TenantId | string? | Resolved from `ITenantAccessor` at write time |
| TransactionId | Guid? | null when outside a domain transaction (DefaultOutboxContext use case) |
| CreationTime | DateTimeOffset | UTC write time |
| Deliveries | ICollection\<OutboxDelivery\> | Navigation (cascade delete) |

### OutboxDelivery

| Field | Type | Notes |
|-------|------|-------|
| DeliveryId | Guid | PK |
| EventId | Guid | FK → OutboxEvent |
| PublisherKey | string | e.g., `"local"`, `"rabbitmq"` |
| Destination | string | Exchange, queue, or empty |
| RoutingKey | string? | Optional routing key |
| State | DeliveryState | NotPublished → InProgress → Published / Failed / Skipped |
| RetryCount | int | Incremented on each failure |
| NextAttemptOn | DateTimeOffset? | Set by delivery policy on failure |
| ProcessedOn | DateTimeOffset? | Set when InProgress begins |

**Indexes** (partial, for delivery workers):
- `IX_OutboxDeliveries_Pending` — State = NotPublished
- `IX_OutboxDeliveries_Retry` — State = Failed AND NextAttemptOn IS NOT NULL
- `IX_OutboxDeliveries_Recovery` — State = InProgress

---

## DI Registration Map

```
AddDefaultMessageService(configure)                         ← Juice.Messaging.Outbox.EF
│
├── DbContext<DefaultOutboxContext>          scoped         ← new
├── IOutboxRepository<>                     scoped (open generic) ← via AddOutbox()
├── IOutboxService<DefaultOutboxContext>    scoped          ← OutboxEventService<DefaultOutboxContext>
├── IMessageService                         scoped          ← MessageService<DefaultOutboxContext>
└── IPostCommitActions                      scoped          ← PostCommitActions (unused but symmetric)

AddDefaultMessageService(configure, autoWireDelivery: true) ← Juice.Messaging.Outbox.Delivery
│
└── [all of the above] +
    └── DeliveryHostedService<DefaultOutboxContext>  singleton (hosted) ← for "local" publisher
```

---

## Schema Compatibility

`DefaultOutboxContext` → `ConfigureOutbox()` → identical EF model to `OutboxContext`

```
Database (any SQL Server or PostgreSQL instance with OutboxContext migrations applied)
├── OutboxEvents          ← written by DefaultOutboxContext
└── OutboxDeliveries      ← read by DeliveryHostedService<DefaultOutboxContext>
```

Multiple contexts writing to the same tables is valid — `DeliveryHostedService` filters by `PublisherKey`, and `OutboxRepository` marks records `InProgress` with optimistic concurrency guards.
