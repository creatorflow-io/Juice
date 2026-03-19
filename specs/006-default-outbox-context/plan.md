# Implementation Plan: DefaultOutboxContext — Full-Route IMessageService

**Branch**: `006-default-outbox-context` | **Date**: 2026-03-18 | **Spec**: [spec.md](spec.md)

## Summary

Introduce `DefaultOutboxContext` — a standalone `DbContext` implementing `IOutboxContext` — and a `AddDefaultMessageService()` extension that backs `IMessageService` with `MessageService<DefaultOutboxContext>`, enabling full-route publishing (local-channel, local, broker) from any code outside a domain transaction. The context lives in `Juice.Messaging.Outbox.EF`; the delivery-aware overload lives in `Juice.Messaging.Outbox.Delivery` to preserve layer order.

---

## Technical Context

**Language/Version**: C# on .NET 6 / 8 / 9 (`$(AppTargetFramework)`)
**Primary Dependencies**: EF Core (version-matched), `Juice.Messaging.Outbox.EF`, `Juice.Messaging.Outbox.Delivery`
**Storage**: SQL Server or PostgreSQL — separate connection from domain DbContexts; tables created by running existing `OutboxContext` migrations
**Testing**: xUnit + FluentAssertions; infrastructure-dependent tests guarded with `IgnoreOnCIFact`
**Target Platform**: Library (`core/src/`); tests under `core/test/`
**Project Type**: NuGet library extensions — no new projects; extending `Juice.Messaging.Outbox.EF` and `Juice.Messaging.Outbox.Delivery`
**Performance Goals**: Same as existing outbox path — no new latency-sensitive paths introduced
**Constraints**: Layer order must be preserved: EF layer cannot depend on Delivery layer → splits `AddDefaultMessageService()` across two assemblies

---

## Constitution Check

| Principle | Status | Notes |
|-----------|--------|-------|
| I. Lightweight & Dual-Architecture | ✅ Pass | `DefaultOutboxContext` solves a concrete gap; no speculative abstractions added |
| II. Library-First Composability | ✅ Pass | Extends existing projects; no circular deps; layer order preserved via two-assembly split |
| III. DDD + CQRS | ✅ Pass | Not affected — this is infrastructure, not domain logic |
| IV. Reliable Messaging via Outbox | ✅ Pass | Reinforces the pattern by enabling outbox for non-transactional code; no fire-and-forget |
| V. Multi-Tenancy First | ✅ Pass | `OutboxEventService<TContext>` already carries `ITenantAccessor`; `DefaultOutboxContext` inherits this |

**Layer split rationale** (complexity justification):

| Situation | Resolution |
|-----------|------------|
| `autoWireDelivery: true` requires `DeliveryBuilder` which is above EF in the layer graph | Core extension (`AddDefaultMessageService(configure)`) in `Juice.Messaging.Outbox.EF`; delivery overload (`AddDefaultMessageService(configure, autoWireDelivery)`) in `Juice.Messaging.Outbox.Delivery` — which already depends on EF |

---

## Project Structure

### Documentation (this feature)

```text
specs/006-default-outbox-context/
├── plan.md              ← this file
├── research.md          ← Phase 0 output
├── data-model.md        ← Phase 1 output
├── quickstart.md        ← Phase 1 output
├── contracts/
│   └── api.md           ← Phase 1 output
└── tasks.md             ← Phase 2 (/speckit.tasks)
```

### Source Code

```text
core/src/Juice.Messaging.Outbox.EF/
├── DefaultOutboxContext.cs                          ← NEW
└── DependencyInjection/
    └── OutboxMessagingBuilderExtensions.cs          ← EXTEND (add AddDefaultMessageService overload)

core/src/Juice.Messaging.Outbox.Delivery/
└── DependencyInjection/
    └── DeliveryOutboxBuilderExtensions.cs           ← EXTEND (add AddDefaultMessageService overload with autoWireDelivery)

core/test/Juice.Messaging.Local.Tests/
└── DefaultOutboxContextTests.cs                     ← NEW (unit tests)
```

**No new projects. No new migrations. Existing `OutboxContext` migrations supply the tables.**

---

## Phase 0: Research

See [research.md](research.md).

---

## Phase 1: Design & Contracts

See [data-model.md](data-model.md) and [contracts/api.md](contracts/api.md).

### Key Design Decisions

1. **`DefaultOutboxContext` is standalone, not a subtype of `OutboxContext`**
   `OutboxContext` is `sealed` in `Juice.Messaging.Outbox.Migrations`. `DefaultOutboxContext` implements `IOutboxContext` directly and calls `this.ConfigureOutbox(modelBuilder)` — producing identical table schema.

2. **`AddDefaultMessageService()` is split across two assemblies**
   - `Juice.Messaging.Outbox.EF`: core registration (context + outbox service + `IMessageService`)
   - `Juice.Messaging.Outbox.Delivery`: overload with `autoWireDelivery: bool` parameter
   Both are extension methods on `MessagingBuilder`; callers reference whichever assembly they need.

3. **`IMessageService` registration uses `TryAddScoped`**
   Same pattern as existing `MessagingBuilder.AddMessageService()`. First registration wins; calling both `AddMessageService()` and `AddDefaultMessageService()` is a misconfiguration to be documented.

4. **No new EF migrations**
   `DefaultOutboxContext` uses `ConfigureOutbox()` from `IOutboxContext` extensions, which produces the same `OutboxEvents` / `OutboxDeliveries` tables as `OutboxContext`. Users run `OutboxContext` migrations against the target DB.

5. **`IPostCommitActions` registered but unused**
   `MessageService<DefaultOutboxContext>` accepts it as optional; since `IsManaged` is always `false` for `DefaultOutboxContext`, no post-commit deferral ever executes. Registered for API symmetry.

6. **`AddOutbox()` called internally**
   `AddDefaultMessageService()` calls `builder.AddOutbox()` to ensure `IOutboxRepository<>` (open generic) and delivery intents are registered. Safe because `TryAdd*` guards prevent double-registration.
