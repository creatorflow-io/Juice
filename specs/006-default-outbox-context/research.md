# Research: DefaultOutboxContext — Full-Route IMessageService

**Feature**: 006-default-outbox-context | **Date**: 2026-03-18

---

## Decision 1: DefaultOutboxContext inheritance strategy

**Decision**: Standalone class `DefaultOutboxContext : DbContext, IOutboxContext` — not a subtype of `OutboxContext`.

**Rationale**: `OutboxContext` (in `Juice.Messaging.Outbox.Migrations`) is `sealed`. EF Core also ties `DbContextOptions<T>` to the exact `T`, making transparent inheritance non-trivial. A standalone class that calls `this.ConfigureOutbox(modelBuilder)` produces an identical table schema with zero coupling to the Migrations project.

**Alternatives considered**:
- Remove `sealed` from `OutboxContext` and subtype it — possible but adds coupling to the Migrations project and changes a public type's contract (minor breaking change).
- Use the existing `OutboxContext` directly as the DI key — risk of collision if callers also register `OutboxContext` for a different publisher.

---

## Decision 2: Project location for `AddDefaultMessageService()`

**Decision**: Core overload (no delivery) in `Juice.Messaging.Outbox.EF`; delivery overload in `Juice.Messaging.Outbox.Delivery`.

**Rationale**: The user requested `Juice.Messaging.Outbox.EF`. The layer graph is:
```
Juice.Messaging.Outbox.Delivery → Juice.Messaging.Outbox.EF → Juice.Messaging.Outbox → Juice.Messaging
```
`Juice.Messaging.Outbox.EF` cannot depend on Delivery (would create an upward layer violation). The `autoWireDelivery` path requires `DeliveryBuilder` which lives in Delivery. **Resolution**: split into two overloads. The Delivery project already depends on the EF project, so the overload there can call the EF extension internally.

**Alternatives considered**:
- Fluent chaining: `AddDefaultMessageService().WithAutoDelivery()` — adds a new return type/builder object; more complex than a bool parameter.
- Single method in a new project `Juice.Messaging.Outbox.Default` — unnecessary new package for a small feature.

---

## Decision 3: Migration strategy

**Decision**: No new migrations. Users run existing `OutboxContext` migrations against the target database.

**Rationale**: `DefaultOutboxContext.OnModelCreating` calls `this.ConfigureOutbox(modelBuilder)` — the same extension method `OutboxContext` uses. Table names (`OutboxEvents`, `OutboxDeliveries`) and column mappings are identical. Any database that has `OutboxContext` migrations applied is immediately compatible.

**Schema support**: `DefaultOutboxContext` can optionally accept a `string? schema` constructor parameter (same as `OutboxContext`) so callers can target a non-default schema. This is consistent with the existing pattern.

**Alternatives considered**:
- Provide separate migration assembly for `DefaultOutboxContext` — redundant, doubles migration maintenance for no gain.
- Auto-migrate at startup — not Juice convention; always left to the caller.

---

## Decision 4: IOutboxService<DefaultOutboxContext> registration

**Decision**: `AddDefaultMessageService()` registers `IOutboxService<DefaultOutboxContext>` → `OutboxEventService<DefaultOutboxContext>` and calls `builder.AddOutbox()` to ensure `IOutboxRepository<>` (open generic) is registered.

**Rationale**: `OutboxEventService<TContext>` requires `IOutboxRepository<TContext>`. The open-generic registration from `AddOutbox()` covers `IOutboxRepository<DefaultOutboxContext>` automatically. `AddOutbox()` uses `TryAdd*` guards so double-calling is safe.

**Alternatives considered**:
- Require callers to call `AddOutbox()` separately before `AddDefaultMessageService()` — more boilerplate, easier to forget.
- Register `IOutboxRepository<DefaultOutboxContext>` directly (closed-generic) — works, but bypasses the standard `AddOutbox()` setup that also registers delivery intents.

---

## Decision 5: `autoWireDelivery` wires only the `"local"` publisher

**Decision**: When `autoWireDelivery: true`, the extension calls `delivery.AddDeliveryProcessor<DefaultOutboxContext>("local")` using framework defaults (all three intents: send-pending, retry-failed, recover-timeout).

**Rationale**: `"local"` is the primary use case for `IMessageService` outside a domain transaction. Broker routes (e.g., `"rabbitmq"`) are expected to have their own delivery processor configured explicitly — the default outbox context only needs the in-process path auto-wired.

**Alternatives considered**:
- Auto-wire all publishers resolved from `IMessagePublishingPolicy` — not known at registration time; requires runtime resolution.
- Accept a `string[] publishers` parameter — adds complexity; `"local"` covers 95% of the use case.

---

## Existing Code Reused (No Changes Required)

| Component | Location | Reused As-Is |
|-----------|----------|-------------|
| `IOutboxContext` + `ConfigureOutbox()` | `Juice.Messaging.Outbox.EF` | ✅ |
| `OutboxRepository<TContext>` | `Juice.Messaging.Outbox.EF` | ✅ (open generic) |
| `OutboxEventService<TContext>` | `Juice.Messaging.Outbox` | ✅ |
| `MessageService<TContext>` | `Juice.Messaging` | ✅ |
| `OutboxEntityTypeConfiguration` | `Juice.Messaging.Outbox.EF` | ✅ |
| `OutboxDeliveryEntityTypeConfiguration` | `Juice.Messaging.Outbox.EF` | ✅ |
| `DeliveryBuilder.AddDeliveryProcessor<TContext>()` | `Juice.Messaging.Outbox.Delivery` | ✅ |
| `IPostCommitActions` / `PostCommitActions` | `Juice.Messaging` | ✅ |
