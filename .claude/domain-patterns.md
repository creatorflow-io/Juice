# Domain Model & DDD Patterns

## Entity Hierarchy

```
DynamicObject (System)
  └─ DynamicEntity                        — JObject Properties, indexer, TryGet/SetMember
       └─ DynamicEntity<TKey>             — adds IIdentifiable<TKey> (Id, Name, Disabled)
            └─ DynamicAuditEntity<TKey>   — adds IAuditable (CreatedUser, ModifiedUser, CreatedDate, ModifiedDate)

Entity<TKey> : IIdentifiable<TKey>        — Id, Name, Disabled; Enable/Disable/UpdateName
  └─ AuditEntity<TKey>                    — adds IAuditable

AggregateRoot<TNotification>              — IList<TNotification> DomainEvents, IList<string> ValidationErrors
AuditAggregrateRoot<TKey, TNotification>  — AuditEntity<TKey> + IAggregateRoot + IValidatable
```

### Key Domain Interfaces
| Interface | Location | Purpose |
|-----------|----------|---------|
| `IIdentifiable<TKey>` | Juice.Domain | Id + Name |
| `IAuditable` | Juice.Domain | CreatedUser/Date, ModifiedUser/Date |
| `ICreationInfo` | Juice.Domain | CreatedUser, CreatedDate |
| `IModificationInfo` | Juice.Domain | ModifiedUser, ModifiedDate |
| `IExpandable` | Juice.Domain | Dynamic JObject Properties |
| `IRemovable` | Juice.Domain | Soft delete |
| `IAggregateRoot<T>` | Juice.Domain | DomainEvents collection |
| `IRepository<T>` | Juice.Domain | Generic repo abstraction |
| `IUnitOfWork` | Juice.Domain | HasActiveTransaction, BeginTransaction, CommitTransaction |

---

## DynamicEntity Pattern
Entities can have schema-less extra properties stored as JSON:
```csharp
// Entity definition
public class Product : DynamicAuditEntity<Guid>
{
    public decimal Price
    {
        get => GetProperty<decimal>();
        set => SetProperty(value);
    }
}
// EF mapping stores Properties as a JSON column
```
- `GetProperty<T>([CallerMemberName])` reads from `JObject Properties`
- `SetProperty<T>(value, [CallerMemberName])` writes and tracks original/current values
- `OriginalPropertyValues` / `CurrentPropertyValues` — for change tracking

---

## Domain Events
### In-process domain events (via MediatR)
```
AuditEvent<T>                          — fired after SaveChanges, carries AuditRecord
DataEvent                              — base class; has IsAudit, Entity, AuditRecord
DataInserted<T>, DataModified<T>, DataDeleted<T>  — typed data change events
```
`AuditRecord` — snapshot of changed property values for audit trail
Flow: DbContextBase.SaveChangesAsync → DispatchEventsAsync(mediator) → dispatch pending events

### Integration events (via Outbox)
```csharp
[Domain("Orders")]   // attribute for routing
public record OrderCreatedEvent : IntegrationEvent
{
    public Guid OrderId { get; init; }
}
```
Flow: Handler adds event to IOutboxService → TransactionBehavior saves to DB outbox → background delivery

---

## EF DbContext Base

### DbContextBase (abstract)
- Extends: `UnitOfWork` (transaction management), `ISchemaDbContext`, `IAuditableDbContext`
- `Schema` — dynamic schema support (per-tenant or per-feature)
- `TenantId`, `User`, `UserPrincipal` — audit context
- `SaveChangesAsync` — calls `TrackingChanges`, `TryUpdateDynamicPropertyAsync`, then `DispatchEventsAsync`
- `ConfigureServices(IServiceProvider)` — called from constructor to inject ILogger, IMediator, DbOptions, ITimeTracker

### UnitOfWork
- `HasActiveTransaction`, `BeginManage()`, `BeginTransaction()`, `CommitTransactionAsync(Guid)`, `ClearEvents()`
- `ResilientTransaction` — wraps EF execution strategy for reliable retry-safe transactions

### IOutboxContext
A DbContext must implement this interface to act as its own outbox store:
```csharp
public interface IOutboxContext
{
    DbSet<OutboxEvent> Outbox { get; }
    DbSet<OutboxDelivery> OutboxDeliveries { get; }
}
```
Alternatively, a factory `Func<TContext, IOutboxContext>` can be provided to use a separate DbContext
that shares the same connection/transaction.

### Schema-Aware Migrations
- `DbSchemaAwareMigrationAssembly` — selects migration by schema
- `DbSchemaAwareModelCacheKeyFactory` — caches EF models per schema

---

## Multi-Tenant Pattern
```
ITenant                  — Id, Identifier, Tier, Name
ITenantAccessor          — Tenant property (scoped, current tenant)
FinbuckleTenantResolver  — resolves tenant from Finbuckle into scoped ITenantAccessor
TenantInfo               — concrete Finbuckle ITenantInfo implementation
MultiTenantDbContext     — DbContextBase + tenant isolation via query filters
```
Tenant data isolation via `SharingType` enum (applied per entity in EF model configuration):
| Value | Behavior |
|-------|---------|
| `None` | Entity is NOT shared — every row must have a tenantId; strict per-tenant isolation |
| `Tenant` | Entity rows with `TenantId == current tenant` OR `TenantId == null` are readable (global/shared rows are read-only for tenants) |
| `Global` | Entity rows are readable/writable by the master tenant (`TenantId == null`), and readable by matching tenant; effectively master-managed shared data |

---

## Audit Attributes
```
[Notice]           — mark property to trigger audit notification
[UpdateDateTime]   — auto-set modified date on change
[UpdateUserInfo]   — auto-set modified user on change
[EntityStates(...)] — control which EF states trigger audit
```

---

## OperationResult Pattern
```csharp
// Success with data
OperationResult.Success(data)
// Failure
OperationResult.Failed("message")
// Usage in handlers
return OperationResult<Guid>.Success(entity.Id);
```
`IOperationResult` — Succeeded (bool), Message, Exception
`IOperationResult<T>` — adds DataValue
`OperationalFailure` enum: `Success | NotFound | Unauthorized | InvalidArgument | NotImplemented`

## Idempotency Record (consumer side)
`IdempotencyRecord` — Scope (string), Key (string), State (New/Processed/Failed), Result (object?)
Stored in `IdempotencyContext` DbContext. Used by `IdempotencyRequestBehavior<TRequest>` on consumer side.

## IRequest Variants (MediatR Contracts)
| Interface | Use |
|-----------|-----|
| `IRequest` | Void command (no return value) |
| `IRequest<TResponse>` | Command/Query with response |
| `IIdempotentRequest` | Adds `IdempotencyKey` for deduplication |
| `IStreamRequest<TResponse>` | Streaming/async enumerable |

## MediatR Service Lifetimes
- `IMediator` — **Transient** (resolves handlers on each call via cache)
- `IRequestHandler<,>` — **Transient**
- `INotificationHandler<>` — **Transient**
- `IPipelineBehavior<,>` — **Scoped** (when injecting DbContext)
