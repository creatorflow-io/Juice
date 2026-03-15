# Implementation Plan: Transaction-Aware IMessageService

**Branch**: `004-messageservice-transaction-aware` | **Date**: 2026-03-15 | **Spec**: [spec.md](./spec.md)

## Summary

Make `IMessageService<TContext>.PublishAsync` transaction-aware: when called inside a `TransactionBehavior` scope (detected via `IUnitOfWork.IsManaged`), defer the outbox save to the behavior. When called outside a transaction, preserve existing immediate save behavior. No new interfaces, no new projects — a single code change in `MessageService<TContext>` with supporting tests.

## Technical Context

**Language/Version**: C# / .NET 6, 8, 9 (multi-targeted: `net6.0;net8.0;net9.0`)
**Primary Dependencies**: `Juice.Messaging.Local`, `Juice.Messaging.Outbox`, `Juice.EF`, `Juice.MediatR.Behaviors`
**Storage**: No new DB schema — uses existing `OutboxEvents` / `OutboxDeliveries` tables
**Testing**: xUnit + `IgnoreOnCIFact`, `[InitializeMessageContext]`
**Target Platform**: Library — consumed by ASP.NET Core host projects
**Project Type**: Modification to existing NuGet library (`Juice.Messaging.Local`)
**Performance Goals**: Zero overhead when outside transaction; negligible cost of `IsManaged` check inside transaction
**Constraints**: No breaking API changes; no new interfaces; no circular dependencies

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-checked after Phase 1 design.*

| Principle | Status | Notes |
|---|---|---|
| I. Lightweight & Dual-Architecture | ✅ Pass | No new abstractions. Single check against existing `IsManaged` flag. Works in both monolith and microservice modes. |
| II. Library-First Composability | ✅ Pass | Change is contained in `Juice.Messaging.Local` (existing project). No new libraries, no circular dependencies. `IUnitOfWork` is in `Juice` which `Juice.Messaging.Local` already references. |
| III. DDD + CQRS | ✅ Pass | `TransactionBehavior` sequence preserved (SaveChanges → DispatchDomainEvents → SaveOutbox → Commit). `IMessageService` defers to the behavior, not the other way around. |
| IV. Reliable Messaging via Outbox | ✅ Pass | Outbox atomicity is **improved** — events published via `IMessageService` inside a transaction are now correctly tagged with `transactionId` instead of being saved standalone. |
| V. Multi-Tenancy First | ✅ Pass | No tenant-related changes. Tenant context flows unchanged. |

## Project Structure

### Documentation (this feature)

```text
specs/004-messageservice-transaction-aware/
├── plan.md              ← this file
├── research.md          ← Phase 0 output
├── data-model.md        ← Phase 1 output (minimal — no new entities)
└── tasks.md             ← /speckit.tasks output
```

### Source Code

```text
core/src/Juice.Messaging.Local/
│   (existing — modify only)
│   └── Internal/
│       └── MessageServiceT.cs        ← MODIFY: add IsManaged check in PublishAsync

core/test/Juice.Messaging.Local.Tests/
│   (existing — add test)
│   └── TransactionAwareTests.cs      ← NEW: test transaction-aware behavior
```

**Structure Decision**: No new projects. Single file modification (`MessageServiceT.cs`) + one new test file. The change resolves `TContext` from DI and checks `IUnitOfWork.IsManaged` to determine whether to call `SaveEventsAsync` or defer.

---

## Phase 0: Research

### Key Decisions

| Decision | Choice | Rationale |
|---|---|---|
| Transaction detection mechanism | Check `TContext` as `IUnitOfWork { IsManaged: true }` via DI-resolved instance | `IsManaged` is set by `TransactionBehavior.BeginManage()` and is the canonical signal. `HasActiveTransaction` checks raw EF transaction which could be a manual transaction unrelated to outbox. `IsManaged` is purpose-built. |
| How to resolve `TContext` in `MessageService<TContext>` | Inject `TContext` directly (it's already scoped) or resolve via `IServiceProvider` | `TContext` is a scoped DbContext — inject directly in constructor. `MessageService<TContext>` already has `where TContext : class` constraint, and `IUnitOfWork` is in the `Juice` base package already referenced. |
| Immediate channel dispatch inside transaction | Skip for `"local"` routes; allow for `"local-channel"` | `"local"` writes to outbox — cannot dispatch immediately before outbox is committed (data may not be visible to handler). `"local-channel"` is non-durable fire-and-forget — already accepted trade-off per spec 003. |
| `IOutboxService` API change needed? | No — `AddEventAsync` + `SaveEventsAsync` remain unchanged | `MessageService<TContext>` just skips calling `SaveEventsAsync`. The scoped `IOutboxService<TContext>` accumulates events. `TransactionBehavior` calls `SaveEventsAsync(transactionId)` later — it picks up all accumulated events. |

### Alternatives Considered

1. **Add `IsInTransaction` property to `IOutboxService`**: Rejected — leaks transaction state into the outbox service, which is a messaging concern not a DB concern. The UoW pattern already provides this signal.
2. **Use `Database.CurrentTransaction != null` on `TContext`**: Rejected — false positives from manual transactions. `IsManaged` is set explicitly by `TransactionBehavior` and is semantically correct.
3. **Add a new `ITransactionContext` interface**: Rejected — unnecessary abstraction. `IUnitOfWork.IsManaged` already exists and is precisely what we need.

---

## Phase 1: Design

### Modified Flow: `MessageService<TContext>.PublishAsync`

```
PublishAsync(IMessage msg)
  │
  ├─ ResolveAsync(PolicyResolveContext) → routes[]
  │
  ├─ [foreach route with key "local-channel"]
  │     Channel<IMessage>.Writer.TryWrite(msg)     ← always, regardless of transaction
  │
  └─ [foreach route with key "local" or broker]
        _outboxService.AddEventAsync(msg)           ← always stage the event
        │
        ├─ TContext is IUnitOfWork { IsManaged: true }?
        │     YES → return (defer save to TransactionBehavior)
        │     NO  → _outboxService.SaveEventsAsync(null, ct)
        │            if hasLocalOutbox && !hasLocalChannel:
        │                EnqueueLocalChannel(msg)    ← immediate dispatch only outside tx
```

### Constructor Change

```
MessageService<TContext> constructor adds:
  + TContext? context = null   (optional — resolved from DI when TContext is a DbContext)

PublishAsync uses:
  var isManaged = _context is IUnitOfWork { IsManaged: true };
```

`TContext` is constrained as `where TContext : class` in `IMessageService<TContext>`. The constructor takes it as optional — if `TContext` is not registered in DI (unlikely but defensive), it falls back to the existing behavior (save immediately).

### No New Data Model

No new entities, tables, or schemas. The existing `OutboxEvent` and `OutboxDelivery` tables are unchanged. The `transactionId` field on `OutboxEvent` is already populated by `TransactionBehavior` — this change ensures events from `IMessageService.PublishAsync` are included in that save.

### Critical Implementation Notes

**Scoped `IOutboxService<TContext>` sharing**: Both `TransactionBehavior` and `MessageService<TContext>` resolve `IOutboxService<TContext>` from the same DI scope. They share the same `OutboxEventService<TContext>` instance, which has a single `_messages` list. Events added by `IMessageService.PublishAsync` (via `AddEventAsync`) are accumulated in the same list as events added directly by domain event handlers. When `TransactionBehavior` calls `SaveEventsAsync(transactionId)`, it persists **all** accumulated events.

**No `_messages.Clear()` conflict**: `SaveEventsAsync` clears `_messages` after saving. When `MessageService` defers (doesn't call `SaveEventsAsync`), the messages remain in the list until `TransactionBehavior` saves them. This is correct — no double-save, no orphaned messages.

**`"local-channel"` inside transaction**: Enqueuing to the channel is allowed even inside a transaction. The handler may run before the transaction commits and see stale data. This is documented and accepted in spec 003 — `"local-channel"` is explicitly non-durable and non-transactional.

---

## Complexity Tracking

No violations. No new projects, interfaces, or abstractions. Single behavioral change in existing code.
