# Research: Transaction-Aware IMessageService

**Date**: 2026-03-15 | **Branch**: `004-messageservice-transaction-aware`

---

## R1: Transaction Detection Mechanism

**Decision**: Use `IUnitOfWork.IsManaged` property on the resolved `TContext` instance.

**Rationale**: `IsManaged` is set by `TransactionBehavior.BeginManage()` at the start of the managed transaction scope (before `next()` is called). It is the canonical signal that the current DbContext is being managed by the pipeline behavior. Alternatives:

- `HasActiveTransaction` (`Database.CurrentTransaction != null`) — checks raw EF transaction, which could be a manual transaction created by application code unrelated to the outbox pattern. False positives.
- `Database.CurrentTransaction.TransactionId` — requires EF dependency in the detection logic and still can't distinguish managed vs. manual transactions.
- Custom `AsyncLocal<bool>` flag — unnecessary complexity when `IsManaged` already exists at the right abstraction level.

**Alternatives considered**:
1. Add `bool IsInTransaction` to `IOutboxService` — rejected: leaks transaction awareness into the messaging layer, which should not know about DB transactions.
2. Add `ITransactionScope` interface — rejected: YAGNI, `IUnitOfWork.IsManaged` is sufficient.

---

## R2: Constructor Injection of TContext

**Decision**: Inject `TContext` as an optional constructor parameter in `MessageService<TContext>`.

**Rationale**: `TContext` is a scoped service (DbContext). The generic constraint is `where TContext : class`, so it may or may not implement `IUnitOfWork`. The `is IUnitOfWork { IsManaged: true }` pattern check handles both cases:
- If `TContext` implements `IUnitOfWork` (the normal case with `DbContextBase`), the check works.
- If `TContext` does not implement `IUnitOfWork` (unusual), the pattern match fails and falls back to the existing behavior (save immediately).
- If `TContext` is not resolvable from DI (defensive edge case), the parameter defaults to `null` and falls back to save immediately.

This approach requires no changes to `IMessageService<TContext>` interface or `IOutboxService`.

**Alternatives considered**:
1. Resolve `TContext` via `IServiceProvider.GetService<TContext>()` at publish time — rejected: constructor injection is simpler, testable, and follows established DI patterns.
2. Pass transaction state via `IOutboxService` — rejected: see R1.

---

## R3: Immediate Dispatch Suppression Inside Transaction

**Decision**: When inside a managed transaction, suppress immediate channel dispatch for `"local"` routes. Allow `"local-channel"` dispatch regardless.

**Rationale**:
- `"local"` writes to the outbox. If we dispatch immediately before the transaction commits, the handler may read data that hasn't been committed yet — or the transaction may roll back, leaving the handler having processed phantom data. After rollback, the outbox record is also gone, so no retry occurs.
- `"local-channel"` is non-durable by design. It never writes to the outbox. The channel dispatch is fire-and-forget. This trade-off is already accepted in spec 003.
- After the transaction commits, `DeliveryHostedService` picks up the outbox records and dispatches via `LocalTransportPublisher` — this is the correct, safe path for `"local"` routes inside transactions.

**Alternatives considered**:
1. Dispatch `"local"` immediately inside transaction anyway (relying on idempotency) — rejected: handler sees uncommitted data; if transaction rolls back, handler already processed invalid state.
2. Post-commit hook to dispatch immediately after commit — deferred: adds complexity to `TransactionBehavior`; the delivery service polling interval is acceptable for transactional flows.

---

## R4: Shared IOutboxService Instance Verification

**Decision**: Rely on the existing scoped DI lifetime — no changes needed.

**Rationale**: Both `TransactionBehavior` and `MessageService<TContext>` resolve `IOutboxService<TContext>` from the same DI scope. `OutboxEventService<TContext>` is registered as scoped, so both receive the same instance with a shared `_messages` list. Events added by `MessageService<TContext>.PublishAsync` (via `AddEventAsync`) accumulate in the same list as events added directly by domain event handlers. When `TransactionBehavior` calls `SaveEventsAsync(transactionId)`, all accumulated events are saved atomically.

Verified by reading:
- `OutboxServiceCollectionExtensions.AddOutboxCore()`: registers `IOutboxService<>` as scoped
- `OutboxEventService<TContext>._messages`: instance-level `IList<IMessage>`
- `SaveEventsAsync`: clears `_messages` after saving — no double-save risk
