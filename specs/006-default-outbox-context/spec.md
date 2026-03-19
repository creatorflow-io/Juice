# Feature Specification: DefaultOutboxContext — Full-Route IMessageService

**Feature Branch**: `006-default-outbox-context`
**Created**: 2026-03-18
**Status**: Draft

## Clarifications

### Session 2026-03-18

- Q: Are separate EF migrations needed for `DefaultOutboxContext`? → A: No — users reuse existing `OutboxContext` migrations; no new migration project required.
- Q: Should delivery infrastructure be auto-registered by `AddDefaultMessageService()`? → A: Yes, optionally — auto-wire is opt-in; callers who want it enabled can activate it; callers who want full manual control can skip it.
- Q: How is the opt-in delivery auto-wire expressed in the API? → A: `AddDefaultMessageService(configure, autoWireDelivery: false)` — boolean parameter, defaults off.
- Q: Which project hosts `DefaultOutboxContext` and `AddDefaultMessageService()`? → A: `Juice.Messaging.Outbox.EF` — extend existing EF outbox project.

## Overview

Today, the non-generic `IMessageService` only handles `"local-channel"` routes (in-memory dispatch) and silently ignores `"local"` (outbox-backed in-process) and broker routes. Any code that lives outside a domain command handler — controllers, background services, integration adapters — cannot publish durable messages without depending on a specific domain `DbContext`.

This feature introduces `DefaultOutboxContext`: a standalone, dedicated outbox `DbContext` with its own separate database connection. Registering it via `AddDefaultMessageService()` replaces the limited `IMessageService` implementation with one backed by `MessageService<DefaultOutboxContext>`, giving callers full routing capability without coupling to any domain context.

---

## User Scenarios & Testing

### User Story 1 — Publish from outside a domain transaction (Priority: P1)

A developer calls `IMessageService.PublishAsync(message)` from a controller action or background service. The message has a `"local"` or broker route configured in publishing policy. The message must be durably written to the outbox and eventually delivered — not silently dropped.

**Why this priority**: This is the core gap being closed. All other stories depend on this working.

**Independent Test**: Register `AddDefaultMessageService(configure)` without any domain `DbContext`. Call `IMessageService.PublishAsync()` with a `"local"` route. Verify the outbox record is written and delivery succeeds.

**Acceptance Scenarios**:

1. **Given** `AddDefaultMessageService(opts => opts.UseSqlServer(...))` is called, **When** `IMessageService.PublishAsync(message)` is called and policy resolves a `"local"` route, **Then** the message is written to `DefaultOutboxContext`'s outbox tables and `SaveEventsAsync` is called immediately.
2. **Given** the same setup, **When** policy resolves a broker (e.g., `"rabbitmq"`) route, **Then** the message is written to the outbox for delivery by the configured `DeliveryHostedService<DefaultOutboxContext>`.
3. **Given** the same setup, **When** policy resolves a `"local-channel"` route, **Then** the message is enqueued directly to the in-memory channel (no outbox write).
4. **Given** `AddDefaultMessageService()` is NOT called and legacy `AddMessageService()` is used, **When** `IMessageService.PublishAsync()` is called with a `"local"` route, **Then** the message is silently ignored (unchanged legacy behavior).

---

### User Story 2 — Coexistence with domain-aware IMessageService\<TContext\> (Priority: P2)

A developer uses both `IMessageService` (for code outside transactions) and `IMessageService<AppDbContext>` (for code inside a `TransactionBehavior`-managed command handler). Both must work independently without interfering.

**Why this priority**: Most non-trivial applications have both domain command handlers and auxiliary code. Both publishing paths must coexist cleanly.

**Independent Test**: Register both `AddDefaultMessageService()` and `AddMessageService<AppDbContext>()`. Inject each in their respective contexts and verify independent operation.

**Acceptance Scenarios**:

1. **Given** both services are registered, **When** `IMessageService<AppDbContext>` is used inside a managed transaction, **Then** outbox save is deferred to `TransactionBehavior` (uses `AppDbContext`).
2. **Given** both services are registered, **When** `IMessageService` is used outside any transaction, **Then** outbox save happens immediately to `DefaultOutboxContext` (separate DB).
3. **Given** both services are registered, **When** both publish the same event type, **Then** no DI conflict occurs and each writes to its own outbox context independently.

---

### User Story 3 — Configure delivery for DefaultOutboxContext (Priority: P3)

A developer wants `DefaultOutboxContext` outbox records drained automatically. They can either activate the opt-in auto-wire inside `AddDefaultMessageService()`, or configure delivery explicitly with `AddDeliveryProcessor<DefaultOutboxContext>()` for full control.

**Why this priority**: Without delivery, outbox records accumulate forever. Offering an opt-in auto-wire reduces boilerplate for the common case while keeping the explicit path available for advanced configuration.

**Independent Test**: Activate auto-wire delivery inside `AddDefaultMessageService()`. Verify `DeliveryHostedService<DefaultOutboxContext>` is registered and drains records written by `IMessageService`.

**Acceptance Scenarios**:

1. **Given** auto-wire delivery is activated in `AddDefaultMessageService()`, **When** `IMessageService.PublishAsync()` writes a `"local"` outbox record, **Then** `DeliveryHostedService<DefaultOutboxContext>` picks it up and dispatches to in-process handlers without any additional delivery configuration.
2. **Given** auto-wire is NOT activated and `AddDeliveryProcessor<DefaultOutboxContext>("local", ...)` is called explicitly, **When** `IMessageService.PublishAsync()` writes a `"local"` outbox record, **Then** the explicitly configured `DeliveryHostedService<DefaultOutboxContext>` processes it.
3. **Given** neither auto-wire nor explicit delivery is configured, **When** `IMessageService.PublishAsync()` is called, **Then** outbox records are written and persist durably — no delivery occurs, no data loss, no exception.

---

### Edge Cases

- What happens when `DefaultOutboxContext` cannot connect to the database? → `SaveEventsAsync` throws; the exception propagates to the caller (same behavior as any `DbContext` connection failure).
- What happens when both legacy `AddMessageService()` and `AddDefaultMessageService()` are called? → `TryAddScoped` semantics mean the first registration wins; calling both is a misconfiguration and should be documented.
- What happens when `AddDefaultMessageService()` is called but the DB tables do not exist (migrations not run)? → First outbox write throws a DB error; caller receives the exception — no silent data loss.
- What if the caller never calls `AddDefaultMessageService()` but injects `IMessageService`? → Falls back to the legacy `MessageService` base (local-channel only); no regression.
- What if `DefaultOutboxContext` and a domain `OutboxContext` are configured to the same DB? → Technically valid; they write to the same tables. Delivery processors for each context will independently process records with matching `PublisherKey`.

---

## Requirements

### Functional Requirements

- **FR-001**: The framework MUST provide a `DefaultOutboxContext` type that is a distinct subtype of `OutboxContext`, inheriting its table schema and EF configuration, usable as a separate DI registration key.
- **FR-002**: `DefaultOutboxContext` MUST accept its own database connection, configured independently of any domain `DbContext` or other `OutboxContext` instance (separate connection string).
- **FR-003**: `DefaultOutboxContext` MUST NOT be treated as managed by `TransactionBehavior` — `IsManaged` is always `false`, so all outbox saves via `DefaultOutboxContext` execute immediately as standalone operations.
- **FR-004**: The framework MUST provide a `MessagingBuilder.AddDefaultMessageService(Action<DbContextOptionsBuilder> configure)` extension method.
- **FR-005**: `AddDefaultMessageService()` MUST register `DefaultOutboxContext` as a scoped `DbContext` with the provided options, and MUST register `IMessageService` as `MessageService<DefaultOutboxContext>`.
- **FR-006**: When registered via `AddDefaultMessageService()`, `IMessageService` MUST handle all route types: `"local-channel"` (in-memory, no DB), `"local"` (immediate write to `DefaultOutboxContext`), and broker routes (write to `DefaultOutboxContext`).
- **FR-007**: `AddDefaultMessageService(configure, autoWireDelivery: false)` MUST support opt-in auto-registration of `DeliveryHostedService<DefaultOutboxContext>` for the `"local"` publisher key via the `autoWireDelivery` boolean parameter (defaults to `false`). When `false`, delivery infrastructure remains the caller's explicit responsibility via `AddDeliveryProcessor<DefaultOutboxContext>()`.
- **FR-009**: `DefaultOutboxContext` and any `IMessageService<TContext>` registrations MUST coexist without DI conflicts or shared outbox state.
- **FR-010**: When `AddDefaultMessageService()` is called but `AddLocalChannel()` has not been called, `"local-channel"` dispatch MUST gracefully no-op (log warning, no exception).

### Key Entities

- **DefaultOutboxContext**: Standalone `DbContext` subtyping `OutboxContext`. Owns `OutboxEvent` and `OutboxDelivery` tables in a dedicated, separately-configured database. Never `IsManaged`. Registered as scoped per request.
- **OutboxEvent**: Record of a serialized message queued for delivery. Carries payload bytes, routing headers, tenant ID, destination, and publisher key. *(Existing — inherited from `OutboxContext`.)*
- **OutboxDelivery**: Per-publisher delivery attempt record linked to an `OutboxEvent`. Tracks state (pending → in-progress → published / failed / skipped) and retry count. *(Existing — inherited from `OutboxContext`.)*

---

## Success Criteria

### Measurable Outcomes

- **SC-001**: A developer can publish a durable message from outside a domain transaction using only `IMessageService` with no changes to call-site code compared to today's usage.
- **SC-002**: Outbox records written via `DefaultOutboxContext` are fully processed by `DeliveryHostedService<DefaultOutboxContext>` with no data loss when delivery is configured.
- **SC-003**: Registering both `IMessageService` (default outbox) and `IMessageService<AppDbContext>` (domain-aware) in the same application produces zero DI errors and zero cross-context interference at runtime.
- **SC-004**: The complete setup — `AddDefaultMessageService` + connection string + `AddDeliveryProcessor` — requires no more than 5 lines of configuration code.
- **SC-005**: All existing tests for `IMessageService<TContext>` transaction-aware behavior and local-channel dispatch pass unchanged after this feature is introduced.

---

## Assumptions

- The existing `OutboxContext` base class and its EF entity configurations (`OutboxEntityTypeConfiguration`, `OutboxDeliveryEntityTypeConfiguration`) are stable and can be inherited by `DefaultOutboxContext` without modification.
- `DefaultOutboxContext` and `AddDefaultMessageService()` live in `Juice.Messaging.Outbox.EF`, which already carries all required EF Core and outbox dependencies.
- `DefaultOutboxContext` does not introduce new migrations. Its outbox tables are created by running the existing `OutboxContext` migrations against the same (or a separate) database schema.
- `IPostCommitActions` is not needed for `DefaultOutboxContext` — since `IsManaged` is always `false`, no post-commit deferral is possible or required.
- The `configure` delegate signature follows the standard EF Core `DbContextOptionsBuilder` pattern, consistent with all other `DbContext` registrations in the framework.
