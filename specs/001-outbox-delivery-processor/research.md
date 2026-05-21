# Research: OutboxDelivery Host/App Tracking Column

## Decision 1: Where to define the node identity abstraction

**Decision**: Add `IDeliveryNodeIdentity` interface to `Juice.Messaging.Outbox` (the contracts layer). Register the default implementation (`DeliveryNodeIdentity`) in `Juice.Messaging.Outbox.Delivery` (the DI wiring layer).

**Rationale**: The interface belongs alongside `IOutboxRepository` — it is a delivery infrastructure concern that needs to be visible to `OutboxRepository<TContext>` in `Juice.Messaging.Outbox.EF`. Placing it in the deepest relevant layer (`Juice.Messaging.Outbox`) keeps the dependency graph clean: EF and Delivery both reference it, neither needs to reference the other.

**Alternatives considered**:
- Define in `Juice.Messaging.Outbox.EF` — rejected: `EF` should not export delivery abstractions; it is a persistence concern, not an identity concern.
- Pass `processedBy` as a parameter on every `IOutboxRepository` Mark* method — rejected: changes the public interface contract and forces all callers (including `LocalChannelBackgroundService`) to acquire and pass the string at every call site. DI injection is cleaner and more consistent with existing patterns.

---

## Decision 2: Node identity value format

**Decision**: Default format is `"{Environment.MachineName}:{Environment.ProcessId}"`. This is computed once at startup and cached. Apps may override via an `IDeliveryNodeIdentity` custom registration.

**Rationale**: Machine name + process ID uniquely identifies one running process on any host without requiring external configuration. `Environment.ProcessId` is available from .NET 6+, matching the framework's minimum target. The value is deterministic for the process lifetime (FR-008).

**Alternatives considered**:
- Machine name only — rejected: multiple app instances on the same host are indistinguishable.
- GUID generated at startup — rejected: not human-readable; operators cannot correlate with host logs.
- Configurable instance name only — rejected: requires all deployments to set configuration; poor default experience.

---

## Decision 3: Column length

**Decision**: Use a dedicated max length of `LengthConstants.NameLength` (128 chars) for `ProcessedBy`. DNS hostnames are capped at 253 chars, but NetBIOS/Windows machine names are max 15 chars and Linux hostnames are conventionally short. 128 chars comfortably fits `MachineName:ProcessId` and leaves room for custom instance names.

**Rationale**: `LengthConstants.IdentityLength` (64 chars) — used for `PublisherKey`, `Destination`, `RoutingKey` — is too short. DNS max hostnames (253 chars) would waste storage. 128 (`NameLength`) is the established Juice medium-length constant and is appropriate here.

**Alternatives considered**:
- Reuse `IdentityLength` (64) — rejected: too short for long hostnames or custom instance names.
- 256 chars — rejected: over-provisioned for this use case; 128 is consistent with existing Juice constants.

---

## Decision 4: Where to set the value (repository vs. entity method)

**Decision**: `OutboxRepository<TContext>` is updated to accept `IDeliveryNodeIdentity` via constructor injection and adds `SetProperty(e => e.ProcessedBy, ...)` to the `ExecuteUpdateAsync` calls in `MarkAsInProgressAsync`, `MarkAsFailedAsync`, and `MarkAsSkippedAsync`. The `OutboxDelivery` entity gains the backing property only; `UpdateState()` is not changed (the repository uses `ExecuteUpdateAsync` which bypasses the entity method).

**Rationale**: The existing Mark* methods already use `ExecuteUpdateAsync` (bulk update without loading the entity), so the cleanest approach is to add the property to the entity and extend the existing `SetProperty` chains. No changes to the `IOutboxRepository` interface signature are needed, avoiding a breaking change.

**Alternatives considered**:
- Add `processedBy` parameter to `IOutboxRepository` methods — rejected: breaks the public interface; all existing custom implementations would require updates.
- Set only on `MarkAsInProgressAsync` — rejected: FR-004/FR-005 require it to be written on retry and skip too.

---

## Decision 5: Breaking-change classification

**Decision**: This is a non-breaking additive change. The `IOutboxRepository` interface is unchanged. `OutboxDelivery` gains a new nullable property (no existing constructor parameters affected). EF migrations are additive (nullable column with no default constraint). Version bump: MINOR.

**Rationale**: Nullable column with a null default is safe for existing rows. No interface signature changes. New constructor parameter on `OutboxRepository` uses DI injection (not consumer-facing).

---

## Findings: Existing migration patterns

The existing `AddRoutingKeyToDelivery` migration (2026-03-03) is the direct precedent. It:
- Adds a nullable `nvarchar(64)` column to `OutboxDeliveries`
- Uses `ISchemaDbContext` for schema injection
- Has matching SqlServer and PostgreSQL variants
- Has no `Down()` side effect other than `DropColumn`

The new `AddProcessedByToDelivery` migration will follow the identical pattern with `nvarchar(128)` / `varchar(128)`.
