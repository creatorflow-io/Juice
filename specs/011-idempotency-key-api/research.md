# Research: Support API Idempotency-Key Header (Server Side)

**Feature**: `011-idempotency-key-api` (based on `release/10`)
**Created**: 2026-07-12
**Purpose**: Map the existing Juice idempotency components, identify what is reusable, and enumerate the gaps the HTTP `Idempotency-Key` feature must close.

## Summary

Juice already contains a complete idempotency subsystem wired to the **MediatR / message-bus** entry point, not HTTP. The core contract (`IIdempotencyService`) and its four stores are directly reusable. New work: an **ASP.NET Core entry point** plus closing durable-store gaps (retention/purge, request-fingerprint conflict detection, in-progress timeout, configurable TTL).

## Existing Components (reuse map)

| Component | Path | Reuse |
|---|---|---|
| `IIdempotencyService` | `core/src/Juice.Messaging/Idempotency/IIdempotencyService.cs` | **Extend** — add `TryBeginRequestAsync(scope, key, requestHash)`; the legacy `TryCreateRequestAsync` overloads were later removed once all callers migrated |
| `IdempotencyRequestBehavior<TRequest,TResponse>` | `core/src/Juice.MediatR.Behaviors/IdempotencyRequestBehavior.cs` | **Reference pattern** — HTTP layer mirrors create → run → complete (`Order = int.MinValue`) |
| `IIdempotentRequest` | `core/src/Juice.MediatR.Contracts/IIdempotentRequest.cs` | Reuse — `string IdempotencyKey`, caller-generated |
| InMemory / DistributedCache store | `core/src/Juice.Messaging.Idempotency.Caching/` | Reuse; lift hard-coded TTLs to config |
| Redis store | `core/src/Juice.Messaging.Idempotency.Redis/` | Reuse; lift hard-coded TTLs to config |
| EF store | `core/src/Juice.Messaging.Idempotency.EF/` | Reuse + **extend** (fingerprint, expiry, in-progress, purge) |
| EF migrations | `core/src/Juice.Messaging.Idempotency.EF.SqlServer/`, `...EF.PostgreSQL/` | **Add migrations** for schema changes (Constitution: both providers) |
| `IdempotencyRecord` | `core/src/Juice.Messaging.Idempotency.EF/IdempotencyRecord.cs` | Extend schema |
| `RequestState` | `core/src/Juice.Messaging.Idempotency.EF/RequestState.cs` | Extend (`New/Processed/Failed` → add in-progress) |
| `DeliveryHostedService<TContext>` | `Juice.Messaging` (outbox delivery) | **Pattern to copy** for purge/recovery background service |

## Current behavior

- **Identity** = `scope + key`. MediatR side: `scope = typeof(TRequest).Name`, `key = request.IdempotencyKey`. HTTP side will use `scope = endpoint/route id`, `key = Idempotency-Key header`.
- **Flow** (`IdempotencyRequestBehavior`): `TryCreateRequestAsync` → if not created, return cached result; else run handler → `TryCompleteRequestAsync(success, result)`; on exception, complete with `success=false`.
- **Failure semantics**: a failed attempt is removed/reset so a retry can re-execute (EF resets `Failed → New` via `TryRetryAsync`; cache stores delete the keys).
- **Concurrency**: EF relies on the unique constraint on `(Scope, Key)` — a racing second insert throws `DbUpdateException`, reported as "already exists". Cache/Redis use `When.NotExists`.

## Retention window — current state

| Store | In-flight TTL | Completed TTL | Cleanup |
|---|---|---|---|
| Redis | 15 min (hard-coded) | **24h (hard-coded)** | Redis native expiry |
| DistributedCache | 15 min (hard-coded) | **24h (hard-coded)** | Cache native expiry |
| EF (SqlServer/PostgreSQL) | none | **none — records persist forever** | **none** |

Implication for the configurable ≥24h requirement (FR-007):
- Cache/Redis satisfy 24h but with magic-number TTLs in three files → lift into a shared `IdempotencyOptions { InFlightTtl, CompletedRetention }`.
- The EF durable store has no retention at all and grows unbounded → add `ExpiresAt` + index + background purge.

## Gaps to close (feature scope)

| # | Gap | Nature | FRs |
|---|---|---|---|
| 1 | ASP.NET Core entry point (attribute/middleware reading `Idempotency-Key`, enforcing presence, bridging to `IIdempotencyService`, emitting 400/409/replay/422) | New library `core/src/Juice.AspNetCore.Idempotency` (or similar) | FR-001, FR-002, FR-003, FR-004, FR-012 |
| 2 | HTTP response capture & replay (persist `{status, contentType, body, headers}` in the record's result slot) | New | FR-003 |
| 3 | Configurable retention (`IdempotencyOptions`), replacing hard-coded TTLs | Change (cache/Redis) + add (EF) | FR-007 |
| 4 | Durable purge background service (copy `DeliveryHostedService` pattern) + `ExpiresAt` column + index | New | FR-009, SC-006 |
| 5 | Request-fingerprint conflict detection (`RequestHash` column, compare on create) | New | FR-005 |
| 6 | Explicit in-progress state + timeout recovery for EF (parity with cache 15-min TTL) | Change | FR-004, FR-010 |
| 7 | EF migrations for SqlServer + PostgreSQL covering fingerprint/expiry/state | New | FR-014 |

## Decisions (resolving spec open questions)

- **Decision**: Target the **EF durable store first** (largest gap); maintain cache/Redis parity via shared options.
  - **Rationale**: EF path lacks retention, purge, fingerprint, and in-progress recovery entirely; cache stores are close to compliant.
  - **Alternatives**: Redis-first (rejected — cache stores already satisfy most requirements; less to prove).
- **Decision**: Key reuse with a different payload is **rejected** (conflict), not treated as a new operation.
  - **Rationale**: Matches industry idempotency semantics (Stripe-style) and FR-005; silent re-execution would defeat the guarantee.
  - **Alternatives**: Treat as new (rejected — hides caller bugs, risks duplicate effects).
- **Decision**: Scope granularity = **per endpoint/route id** by default, overridable via the attribute.
  - **Rationale**: Prevents cross-endpoint key collisions while allowing intentional cross-endpoint idempotency when needed.
  - **Alternatives**: Global scope (rejected — collisions), per-tenant only (covered separately by FR-011).
- **Decision**: Retention default = **24h**, configurable through `IdempotencyOptions`.
  - **Rationale**: Matches the existing cache/Redis completed TTL and common client retry horizons.
  - **Alternatives**: Shorter fixed window (rejected — misses slow retries); unbounded (rejected — storage growth).

## Constitution alignment (Juice)

- **II Library-First**: new HTTP idempotency library under `core/src/`, test under `core/test/`, depends only on abstractions (`IIdempotencyService`), no upward layer dependency.
- **III DDD + CQRS**: reuses the MediatR `IIdempotentRequest`/behavior pattern; the HTTP filter runs before the handler, analogous to `Order = int.MinValue`.
- **V Multi-Tenancy First**: keys scoped via Finbuckle tenant context; EF store tenant-aware (FR-011).
- **Tech constraints**: idempotency store stays pluggable via `IIdempotencyService`; EF migrations for both SQL Server and PostgreSQL; StackExchange.Redis / EF-backed only.
