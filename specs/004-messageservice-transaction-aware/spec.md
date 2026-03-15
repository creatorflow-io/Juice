# Feature Specification: Transaction-Aware IMessageService

**Feature Branch**: `004-messageservice-transaction-aware`
**Created**: 2026-03-15
**Status**: Draft
**Input**: Handle case using IMessageService<T> inside transaction behavior via domain event handler, message service will check outboxservice is inside transaction to decide save event or not

## Overview

When `IMessageService<TContext>.PublishAsync` is called inside a `TransactionBehavior` scope (e.g., from a domain event handler dispatched during the transaction), it currently calls `SaveEventsAsync(null)` immediately — breaking the atomic outbox commit. This feature makes `IMessageService<TContext>` transaction-aware: it detects the active managed transaction and defers the save to `TransactionBehavior`, preserving atomicity.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Publish via IMessageService Inside TransactionBehavior (Priority: P1)

A developer uses `IMessageService<TContext>.PublishAsync` inside a domain event handler that runs within `TransactionBehavior`. The message service detects the active managed transaction and only stages the event via `AddEventAsync` — it does **not** call `SaveEventsAsync`. The `TransactionBehavior` saves all staged events atomically with the domain data when it calls `SaveEventsAsync(transactionId)` at commit time.

**Why this priority**: This is the core problem. Without it, events published via `IMessageService` inside a transaction break atomicity — they are saved with `transactionId=null` and committed independently from the domain data.

**Independent Test**: Inside a `TransactionBehavior` scope, call `IMessageService<TContext>.PublishAsync` from a domain event handler. Verify the outbox record has the correct `transactionId` (not null) and is committed atomically with the domain entity changes.

**Acceptance Scenarios**:

1. **Given** a command handler running inside `TransactionBehavior` raises a domain event, **When** the domain event handler calls `IMessageService<TContext>.PublishAsync`, **Then** the message is staged via `AddEventAsync` only — `SaveEventsAsync` is NOT called by `IMessageService`.
2. **Given** events staged by `IMessageService<TContext>` during domain event dispatch, **When** `TransactionBehavior` reaches its commit phase, **Then** `SaveEventsAsync(transactionId)` persists all staged events (both from direct `IOutboxService.AddEventAsync` and from `IMessageService.PublishAsync`) atomically with domain data.
3. **Given** events staged by `IMessageService<TContext>` during domain event dispatch, **When** the transaction rolls back, **Then** no outbox records are persisted — the staged events are discarded along with the domain changes.

---

### User Story 2 - Publish via IMessageService Outside TransactionBehavior (Priority: P1)

A developer calls `IMessageService<TContext>.PublishAsync` outside any `TransactionBehavior` scope (e.g., from a background job, API controller, or standalone service). The message service detects there is no active managed transaction and calls `SaveEventsAsync(null)` immediately as a standalone operation — existing behavior preserved.

**Why this priority**: Equal to US1 — the outside-transaction path must continue to work correctly. This is the existing behavior that must not regress.

**Independent Test**: Call `IMessageService<TContext>.PublishAsync` without any active transaction. Verify the outbox record is saved immediately with `transactionId=null`.

**Acceptance Scenarios**:

1. **Given** no active `TransactionBehavior` scope, **When** `IMessageService<TContext>.PublishAsync` is called, **Then** the event is staged and saved immediately via `SaveEventsAsync(null)`.
2. **Given** no active `TransactionBehavior` scope with a `"local"` route, **When** `IMessageService<TContext>.PublishAsync` is called, **Then** the event is also enqueued to the in-memory channel for immediate dispatch (existing behavior preserved).

---

### User Story 3 - Local-Channel Route Unaffected by Transaction State (Priority: P2)

A developer publishes a message routed to `"local-channel"` inside a `TransactionBehavior` scope. The `"local-channel"` route bypasses the outbox entirely — it enqueues to the in-memory channel regardless of transaction state. This behavior is unchanged.

**Why this priority**: Ensures the non-durable path is unaffected by the transaction detection logic.

**Independent Test**: Inside a `TransactionBehavior` scope, publish an event with `"local-channel"` route. Verify it is enqueued to the channel immediately and no outbox record is written.

**Acceptance Scenarios**:

1. **Given** an active `TransactionBehavior` scope, **When** `IMessageService.PublishAsync` is called with a `"local-channel"` route, **Then** the event is enqueued to the in-memory channel immediately — no outbox involvement.
2. **Given** a policy that resolves both `"local-channel"` and `"local"` routes, **When** `IMessageService<TContext>.PublishAsync` is called inside a transaction, **Then** the `"local-channel"` portion is enqueued immediately AND the `"local"` portion is staged for deferred save.

---

### Edge Cases

- What happens if `IMessageService<TContext>.PublishAsync` is called after `TransactionBehavior` has already called `SaveEventsAsync(transactionId)` but before `CommitTransactionAsync`? The event would be staged but not saved in this transaction — it would be orphaned. This should not happen in practice because domain event dispatch occurs before the save phase.
- What happens if the `DbContext` has an active transaction but is NOT managed by `TransactionBehavior` (e.g., manual `BeginTransaction`)? The message service should detect the managed state, not just the raw transaction presence.
- What happens if `IMessageService<TContext>` is called multiple times during the same transaction? All events should accumulate in the same `IOutboxService<TContext>` (scoped) and be saved together by `TransactionBehavior`.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: `IMessageService<TContext>.PublishAsync` MUST detect whether the `DbContext` (`TContext`) is currently managed by `TransactionBehavior`. When managed, it MUST only call `AddEventAsync` — it MUST NOT call `SaveEventsAsync`.
- **FR-002**: When `IMessageService<TContext>.PublishAsync` is called outside a managed transaction, it MUST call both `AddEventAsync` and `SaveEventsAsync(null)` immediately — preserving the existing standalone behavior.
- **FR-003**: `"local-channel"` route enqueuing MUST be unaffected by transaction state — it MUST always enqueue to the channel immediately regardless of whether a transaction is active.
- **FR-004**: When inside a managed transaction with a `"local"` route, `IMessageService<TContext>` MUST NOT enqueue to the channel for immediate dispatch. The immediate dispatch MUST only happen in the outside-transaction path (after `SaveEventsAsync` commits the outbox standalone).
- **FR-005**: The transaction detection MUST use the managed state set by `TransactionBehavior` (e.g., `DbContext.BeginManage()` / `HasActiveTransaction`), not raw EF `Database.CurrentTransaction`, to avoid false positives from manual transactions unrelated to the outbox.
- **FR-006**: All events staged via `IMessageService<TContext>.PublishAsync` during a managed transaction MUST be persisted by `TransactionBehavior`'s `SaveEventsAsync(transactionId)` call — tagged with the correct `transactionId` for post-commit delivery.

### Key Entities

- **IMessageService\<TContext\>**: Unified publishing interface. Must become transaction-aware — detect managed transaction and defer save.
- **IOutboxService\<TContext\>**: Scoped service that accumulates events. Shared between `TransactionBehavior` (direct `AddEventAsync` from domain handlers) and `IMessageService<TContext>` (via `PublishAsync`). Same instance within the request scope.
- **TransactionBehavior**: Pipeline behavior that manages the DB transaction, dispatches domain events, and calls `SaveEventsAsync(transactionId)` atomically.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Events published via `IMessageService<TContext>.PublishAsync` inside a `TransactionBehavior` scope are persisted with the correct `transactionId` — verified by querying outbox records after commit.
- **SC-002**: Events published via `IMessageService<TContext>.PublishAsync` inside a `TransactionBehavior` scope are rolled back when the transaction fails — verified by asserting zero outbox records after a failed transaction.
- **SC-003**: Events published via `IMessageService<TContext>.PublishAsync` outside a transaction are saved immediately — verified by querying outbox records before any background delivery runs.
- **SC-004**: Existing tests for `TransactionBehavior`, `IOutboxService`, and `IMessageService` continue to pass without modification.
- **SC-005**: No breaking API changes — `IMessageService`, `IOutboxService`, and `TransactionBehavior` public interfaces remain unchanged.

## Assumptions

- The `IOutboxService<TContext>` instance is **scoped** — the same instance is shared between `TransactionBehavior` and `IMessageService<TContext>` within a single request scope. Events staged by `IMessageService.PublishAsync` are accumulated in the same `_messages` list as events staged by direct `AddEventAsync` calls from domain event handlers.
- `TransactionBehavior` always calls `SaveEventsAsync(transactionId)` after domain event dispatch (step 7 in the flow). Any events added during dispatch (whether via `IOutboxService.AddEventAsync` directly or via `IMessageService.PublishAsync`) will be included.
- The transaction detection mechanism uses the `DbContext`'s managed state (set by `BeginManage()`) rather than raw EF transaction presence, to avoid interference with manual transactions not related to outbox processing.
- `"local-channel"` enqueue inside a transaction is intentionally allowed — it is non-durable by design, and the handler may run before the transaction commits. This is an accepted trade-off documented in the local transport spec.
