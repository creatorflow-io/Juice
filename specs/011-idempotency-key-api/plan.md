# Implementation Plan: Support API Idempotency-Key Header (Server Side)

**Branch**: `011-idempotency-key-api` (based on `release/10`) | **Date**: 2026-07-12 | **Spec**: [spec.md](./spec.md)
**Input**: Feature specification from `/specs/011-idempotency-key-api/spec.md`

## Summary

Extend Juice's existing `IIdempotencyService` idempotency subsystem — today consumed only by the MediatR pipeline (`IdempotencyRequestBehavior`) — to ASP.NET Core HTTP endpoints that require an `Idempotency-Key` header, guaranteeing exactly-once processing of state-changing operations and replaying prior outcomes for retries. The primary approach **reuses the CQRS/MediatR path**: a thin ASP.NET Core filter binds the `Idempotency-Key` header onto commands implementing `IIdempotentRequest`, and the existing behavior enforces idempotency. Separately, the durable EF store is extended to close four gaps — configurable retention, background purge, request-fingerprint conflict detection, and in-progress timeout recovery — with matching SQL Server + PostgreSQL migrations. Cache/Redis stores are brought to parity by lifting their hard-coded TTLs into shared options.

## Technical Context

**Language/Version**: C# (`LangVersion=latest`), .NET 6/8/9/10 (`$(AppTargetFramework)`); idempotency libraries follow the existing EF idempotency project and target the app frameworks (EF + ASP.NET Core needed).
**Primary Dependencies**: Juice.Messaging (`IIdempotencyService`), Juice.MediatR (`IIdempotentRequest`, `IMediator`), EF Core (7/8/9/10 version-matched), Microsoft.AspNetCore.* (`$(AspNetCoreVersion)`), StackExchange.Redis 2.8+ (parity path), Finbuckle.MultiTenant 9.x (tenant scoping).
**Storage**: EF Core `IdempotencyContext` (SQL Server + PostgreSQL), table `IdempotencyRecords` (composite PK `Scope`+`Key`); Redis / DistributedCache / InMemory as pluggable alternates.
**Testing**: xUnit v3 (`$(XUnitV3Version)`); infra-dependent tests guarded with `IgnoreOnCIFact`; messaging tests use `[InitializeMessageContext]`.
**Target Platform**: Server (ASP.NET Core hosts on Linux/Windows), library packages `netstandard`-compatible via app frameworks.
**Project Type**: .NET library feature (framework), per Constitution `core/src/` + `core/test/`.
**Performance Goals**: Idempotency create/check adds a single indexed lookup + insert per state-changing request; replay avoids handler execution. No measurable added latency on the non-duplicate path beyond one keyed DB/cache round-trip.
**Constraints**: No raw SQL bypassing EF migrations; store must stay pluggable via `IIdempotencyService`; EF schema changes require both-provider migrations; multi-tenant isolation required.
**Scale/Scope**: One new library + extensions to five existing idempotency projects + two migration projects. Steady-state record count bounded by retention window × request rate (FR-009).

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Status | Notes |
|---|---|---|
| **I. Lightweight & Dual-Architecture** | ✅ PASS | Feature works in monolith or microservice; HTTP filter depends only on `IIdempotencyService` abstraction, no hosting topology imposed. New abstraction (`IdempotencyOptions`, HTTP filter) each has a concrete use (YAGNI honored). |
| **II. Library-First Composability** | ✅ PASS | New `Juice.AspNetCore.Idempotency` under `core/src/` with test project under `core/test/`; depends downward on `Juice.Messaging` and `Juice.MediatR.Contracts` only (both below it in the layer graph); no concrete cross-library coupling; distinct purpose (HTTP entry point). |
| **III. DDD + CQRS** | ✅ PASS | Primary path dispatches via `Juice.MediatR.IMediator` and reuses `IIdempotentRequest` + `IdempotencyRequestBehavior` (`Order = int.MinValue`, runs before handler); no direct MediatR NuGet dispatch; behaviors stay stateless. `IIdempotencyService` gains one **additive** method (`TryBeginRequestAsync` → `IdempotencyResult`) for the HTTP outcome model; the existing overloads and all four store implementations remain source-compatible. |
| **IV. Reliable Messaging via Outbox** | ✅ PASS (N/A-leaning) | Feature does not publish messages; if idempotent handlers raise domain events, the existing `TransactionBehavior` outbox sequence is untouched. Purge background service follows the `DeliveryHostedService` pattern but does not bypass the outbox. |
| **V. Multi-Tenancy First** | ⚠ DOCUMENTED DEVIATION | `IdempotencyContext` is **not** tenant-aware today. FR-011 requires tenant isolation. Resolved by incorporating the **server-resolved** Finbuckle tenant identifier into the idempotency `Scope` (T015), so records are partitioned per tenant and a caller cannot address another tenant's `(Scope, Key)`. The tenant id **may be null** (non-multi-tenant hosts or requests with no resolved tenant); a null tenant maps to a single well-known unscoped partition and never collides with any real tenant. This is a value-level partition, **not** the physical schema/connection isolation Principle V's literal clause requires — an explicitly accepted deviation, signed off below. See Complexity Tracking. |

**Gate result**: PASS with one design item (V) carried into Phase 1. No unjustified violations.

## Project Structure

### Documentation (this feature)

```text
specs/011-idempotency-key-api/
├── plan.md              # This file
├── research.md          # Reuse map + current-state analysis + decisions
├── data-model.md        # Phase 1 output — IdempotencyRecord/state/options/response
├── quickstart.md        # Phase 1 output — enable + call an idempotent endpoint
├── contracts/           # Phase 1 output — public API + HTTP semantics
│   └── idempotency-http.md
└── checklists/
    └── requirements.md
```

### Source Code (repository root)

```text
core/src/
├── Juice.AspNetCore.Idempotency/            # NEW — HTTP entry point
│   ├── IdempotentAttribute.cs               #   opt-in marker for endpoints/actions
│   ├── IdempotencyKeyActionFilter.cs        #   reads Idempotency-Key header, requires/validates, binds to command
│   ├── IdempotencyEndpointFilter.cs         #   minimal-API equivalent (net8+)
│   ├── ProblemDetails/                       #   400 missing / 409 in-progress / 422 conflict responses
│   └── DependencyInjection/ApiIdempotencyServiceCollectionExtensions.cs
│
├── Juice.Messaging/Idempotency/
│   └── IdempotencyOptions.cs                # NEW — { InFlightTtl, CompletedRetention, MaxKeyLength }
│
├── Juice.Messaging.Idempotency.EF/          # EXTEND
│   ├── IdempotencyRecord.cs                 #   + RequestHash, ExpiresAt, In-progress semantics
│   ├── RequestState.cs                      #   + InProgress
│   ├── IdempotencyContext.cs                #   configure new columns + IX on ExpiresAt
│   ├── IdempotencyService.cs                #   fingerprint compare, expiry-aware reads, in-progress
│   └── IdempotencyPurgeHostedService.cs     # NEW — purge expired + recover timed-out (DeliveryHostedService pattern)
│
├── Juice.Messaging.Idempotency.Caching/     # EXTEND — TTLs from IdempotencyOptions
├── Juice.Messaging.Idempotency.Redis/       # EXTEND — TTLs from IdempotencyOptions
├── Juice.Messaging.Idempotency.EF.SqlServer/  # ADD migration (RequestHash, ExpiresAt, State)
└── Juice.Messaging.Idempotency.EF.PostgreSQL/ # ADD migration (RequestHash, ExpiresAt, State)

core/test/
├── Juice.AspNetCore.Idempotency.Tests/      # NEW — filter behavior, header required, replay, conflict
└── Juice.Messaging.Idempotency.Tests/       # NEW — fingerprint, expiry, purge, in-progress recovery
```

**Structure Decision**: One new library (`Juice.AspNetCore.Idempotency`) at the Infrastructure/Extensions layer plus targeted extensions to the existing idempotency projects. This maximizes reuse (the MediatR behavior and all four stores are unchanged in contract), keeps the HTTP concern in its own composable package (Principle II), and confines schema changes to the EF store + its two migration projects (Constitution: both-provider migrations).

## Complexity Tracking

> Only the Multi-Tenancy design item requires justification.

| Violation / Deviation | Why Needed | Simpler Alternative Rejected Because |
|---|---|---|
| Tenant identity encoded into idempotency `Scope` (rather than full EF schema/connection isolation for the idempotency store) | `IdempotencyContext` is a small, cross-cutting infra store keyed by `(Scope, Key)`; embedding the **server-resolved** tenant in `Scope` gives per-tenant key isolation (FR-011) with no per-tenant connection/schema multiplication for what is transient bookkeeping data. | Full schema/connection-per-tenant isolation (Principle V's strict form) rejected here because it would multiply idempotency DbContexts/migrations per tenant for short-lived records, adding significant operational cost with no data-confidentiality benefit beyond scope partitioning. A `TenantId` column + query filter remains available if a compliance need arises. |

**Deviation sign-off (Principle V):** The tenant identifier is resolved **server-side** from the Finbuckle tenant context and stamped into `Scope`; a client-supplied `tenantId` is **not** trusted for isolation (a caller could forge another tenant's value). The resolved tenant **may be null** — non-multi-tenant hosts or requests with no tenant context resolve to a single well-known unscoped partition, which never collides with a real tenant's scope. This value-level partition is accepted in place of physical schema/connection isolation for this transient, self-purging store.

- Decision: accepted deviation from Principle V's schema/connection clause.
- Approved by: `<maintainer name>` — `<date>` *(fill in at PR review; required before merge per Constitution Governance)*
- Revisit trigger: any compliance requirement mandating physical per-tenant isolation for idempotency data.
