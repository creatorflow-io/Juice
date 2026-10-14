# v10.3.1 - Multi-tenant DbContext fix

## Fixes

### `Juice.EF.MultiTenant`
- `MultiTenantDbContext.TenantInfo` is now resolved from `IMultiTenantContextAccessor` on every access instead of
  being captured once in `ConfigureServices`. Contexts created before tenant resolution (or reused from a pool) no
  longer stay pinned to a stale/`null` tenant, which previously caused query filters and `EnforceTenantPolicies`
  to treat them as the root tenant.
- `TenantInfo` is now get-only (the former `internal` setter is removed).
- Note: a long-lived context follows the ambient tenant if it changes between operations.
- Fixed `TenantInfo.MessageId` → `TenantInfo.Id` in XML docs and comments.

# v10.3.0 - HTTP Idempotency-Key Support (Server Side)

## Features

### New package: `Juice.AspNetCore.Idempotency`
Server-side enforcement of the HTTP `Idempotency-Key` header for state-changing endpoints, reusing the
existing `IIdempotencyService` store abstraction (InMemory / DistributedCache / Redis / EF).

- Opt-in via `[Idempotent]` on a controller action or minimal-API endpoint; unmarked endpoints are unaffected.
- `services.AddApiIdempotency(...)` registers the MVC action filter and the net8+ minimal-API endpoint filter.
- Outcomes: exactly-once execution, replay of the stored response, `409` in-progress, `422` payload conflict,
  `400` missing/invalid key (RFC 7807 ProblemDetails).
- Server-resolved tenant scoping via `IIdempotencyTenantProvider` (tenant may be null → single unscoped partition);
  a client-supplied tenant is never trusted for isolation.

### Extended: durable EF idempotency store
- `IdempotencyRecord` gains `RequestHash` (fingerprint conflict detection), `ExpiresAt` (retention), and
  `LockedAt` (in-progress recovery); `RequestState.InProgress` added.
- `IdempotencyPurgeHostedService` purges expired records and recovers crashed in-flight locks (registered
  automatically by `AddIdempotencyEF`).
- Configurable retention/TTLs via `IdempotencyOptions` (replaces the hard-coded 24h/15m TTLs in the
  Redis and DistributedCache stores).
- New EF migrations `AddIdempotencyRetentionAndFingerprint` for **SQL Server and PostgreSQL**.

### Contract change
`IIdempotencyService` gains an additive method `TryBeginRequestAsync(scope, key, requestHash)` returning
`IdempotencyResult { Outcome, StoredResult }`. All four in-box stores implement it. **Note**: external
implementations of `IIdempotencyService` must add this member.

---

# v9.1.0 - EventBus Consolidation (Non-Breaking)

## Features

### EventBus Contracts Consolidation
`IIntegrationEvent` and `IIntegrationEventHandler<T>` have been moved to `Juice.Messaging.Contracts` to better align with messaging infrastructure.

**No Action Required**: Existing code continues to work because of inheritance.

**Recommended Migration** (for future-proofing):    