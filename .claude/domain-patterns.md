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
| `IValidatable` | Juice.Contracts | `ValidationErrors` list + fluent validation extensions |
| `IDynamic` | Juice.Contracts | `GetProperty<T>`, `SetProperty<T>`, indexer (CallerMemberName) |
| `IMessage` | Juice.Contracts | `MessageId` (Guid), `CreatedAt` (DateTimeOffset), `TenantId` |
| `IEvent : IMessage` | Juice.Contracts | Adds `EventName` |
| `ITenant : IDynamic` | Juice.Contracts | `Id, Name, Identifier, OwnerUser, Tier, Region` |
| `ITenantAccessor` | Juice.Contracts | `ITenant? Tenant` — current tenant |

### IValidatable Fluent API (extension methods in `Juice.Contracts`)
Used by `AggregateRoot` and other entities for domain validation:
```csharp
entity.NotNullOrWhiteSpace(entity.Name)       // CallerArgumentExpression auto-names param
      .NotExceededLength(entity.Name, 256)
      .NotNull(entity.Config)
      .InRange(entity.Price, 0, 1000)
      .NotNegative(entity.Quantity)
      .NotZero(entity.Id)
      .RegexMatch(entity.Email, @"^[\w@.]+$")
      .ValidateJson(entity.Schema)
      .ThrowIfHasErrors();                    // throws ValidationException
```

### MessageBase (abstract record, `Juice.Contracts`)
Base for all integration events. Records are immutable.
```csharp
public abstract record MessageBase : IMessage {
    Guid MessageId { get; init; }          // default: Guid.NewGuid()
    DateTimeOffset CreatedAt { get; init; } // default: UtcNow
    string? TenantId { get; protected set; }
}
```

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
- `TenantId`, `User` (from ClaimsPrincipal NameIdentifier/Name), `UserPrincipal` — audit context
- `SaveChangesAsync` → `TrackingChanges` → `TryUpdateDynamicPropertyAsync` (JSON) → `base.SaveChangesAsync` → `DispatchEventsAsync` (in finally)
- `ConfigureServices(IServiceProvider)` — called from constructor to inject ILogger, IMediator, DbOptions, ITimeTracker
- `ITimeTracker` — optional scope-based execution timing (enabled via `DbOptions.EnableTimeTracking`)
- `abstract ConfigureModel(ModelBuilder)` — entity configuration hook

### UnitOfWork
- `IsManaged` — set by `BeginManage()`; tells context it's behavior-managed
- `HasActiveTransaction` — checks `Database.CurrentTransaction` against stored `_committedTransactionId`
- `BeginManage()`, `BeginTransaction()`, `CommitTransactionAsync(Guid)`, `ClearEvents()`
- `ResilientTransaction` — wraps EF execution strategy for reliable retry-safe transactions
- CRUD helpers: `AddAsync<T>`, `DeleteAsync<T>`, `UpdateAsync<T>`, `FindAsync<T>`, `Query<T>`

### RepositoryBase<T, TContext>
Generic repository pattern (abstract base):
- `IUnitOfWork<T> UnitOfWork` — wraps context if it implements `IUnitOfWork`
- `AddAsync`, `DeleteAsync`, `UpdateAsync` — delegate to DbContext internal save helpers
- `FindAsync(predicate, readOnly)` — with optional `AsNoTracking`
- `ReadAsync<TKey>(id)` — read-only find by ID
- `GetAsync<TKey>(id)` — tracked find by ID
- `ExistsAsync<TKey>(id)` — existence check
- `Query()` → `DbContext.Set<T>()`
- `CreateIdPredicate<TKey>(value)` — builds expression tree; finds key via `[Key]` attr → `"Id"` → `"{TypeName}Id"`

### DynamicEntity EF Integration
- `IsExpandable(EntityTypeBuilder, DbContext)` — adds `Properties` JSON column; PostgreSQL uses `jsonb`
- `ConfigureExpandableEntities(ModelBuilder, DbContext)` — auto-applies to all `IExpandable` entities

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

### MultiTenantDbContext details
- Extends `DbContextBase`; gets `ITenantInfo` from `IMultiTenantContextAccessor`
- `TenantMismatchMode` / `TenantNotSetMode` — Finbuckle enums (Throw/Overwrite/Ignore)
- `SaveChangesAsync` → `EnforceTenantPolicies()` before base save

### EnforceTenantPolicies (at save time)
- **Root tenant** (`TenantInfo == null`): only allows `SharingType.Global` entities
- **Regular tenant**:
  - Added with null TenantId → `TenantNotSetMode.Throw` or `Overwrite` (auto-set)
  - Modified/Deleted with mismatched TenantId → `TenantMismatchMode.Throw` / `Ignore` / `Overwrite`

### IsMultiTenant (EF query filter expression trees)
```csharp
builder.Entity<Product>().IsMultiTenant(SharingType.None);     // strict
builder.Entity<Setting>().IsCrossTenant();                      // = SharingType.Tenant
builder.Entity<GlobalConfig>().IsMultiTenant(SharingType.Global);
```
Builds expression tree: adds `TenantId` shadow property (128 chars), creates query filter combined with any existing filters via AND.

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
Standard return type for all MediatR handlers — Result pattern instead of throwing exceptions.

### Interfaces (`Juice.Contracts`)
- `IOperationResult` — `Succeeded`, `Message`, `StackTrace`, `Failure` (enum), `Exception`, `ThrowIfNotSucceeded()`, `OperationModel`
- `IOperationResult<T>` — adds `Data`, `HasData`, `SucceededWithData`, `DataValue` (throws if null), `OperationModel<T>`
- `OperationModel` / `OperationModel<T>` — serializable record for API responses (excludes Exception)

### `OperationalFailure` enum
`None`, `NotFound`, `Unauthorized`, `NotImplemented`, `InvalidArgument`, `Timeout`

### Static factory (`OperationResult` class in `Juice`)
```csharp
// Success
OperationResult.Success                              // cached singleton
OperationResult.Succeeded("message")
OperationResult.Result<T>(data, "message")           // success + data
OperationResult.Succeeded<T>("message")              // success without data

// Failure
OperationResult.Failed("message")
OperationResult.Failed(exception)
OperationResult.Failed(exception, "message")
OperationResult.Failed<T>("message")

// Semantic failures (auto-captures stack trace)
OperationResult.NotFound(entity)                     // CallerArgumentExpression for name
OperationResult.Unauthorized()                       // auto-gets caller method name
OperationResult.NotImplemented()
OperationResult.ArgumentNull(param)                  // CallerArgumentExpression
OperationResult.ArgumentInvalid(param)

// Conversion
result.Of<T>(data)                                   // IOperationResult → IOperationResult<T>

// Helpers
result.IsNotFound(), result.IsUnauthorized(), result.IsNotImplemented(), result.IsInvalidArgument()
OperationResult.FromJson(json)                       // deserialize
```

### Smart behaviors in `OperationResultInternal`
- Setting `Exception` auto-maps known types → `OperationalFailure`:
  `UnauthorizedAccessException → Unauthorized`, `NotImplementedException → NotImplemented`, `ArgumentNullException → InvalidArgument`
- `ThrowIfNotSucceeded()` re-throws with original stack trace via `ExceptionDispatchInfo`
- Non-exception failures capture 3 stack frames for diagnostics

### Integration with MediatR pipeline
`OperationExceptionBehavior` catches unhandled exceptions → wraps as `OperationResult.Failed(ex)` so handlers never throw to callers.

## LengthConstants (`Juice.Contracts`)
Standard EF column length constants — used in entity configurations for consistency.
```csharp
IdentityLength       = 64    // User IDs, tenant IDs, keys
ShortNameLength      = 64    // Slugs, codes, short identifiers
NameLength           = 256   // Display names, titles
ShortDescriptionLength = 2048  // Descriptions, summaries
```
`Juice.EF.Constants` extends `LengthConstants` and adds EF annotation names:
`AuditAnnotationName = "Juice:Auditable"`, `DynamicExpandableAnnotationName = "Juice:Expandable"`

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
