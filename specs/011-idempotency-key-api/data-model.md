# Data Model: Support API Idempotency-Key Header (Server Side)

**Feature**: `011-idempotency-key-api` | **Date**: 2026-07-12

Extends the existing `IdempotencyRecord` (`core/src/Juice.Messaging.Idempotency.EF/IdempotencyRecord.cs`). New fields are marked **NEW**; existing fields are retained for backward compatibility.

## Entity: IdempotencyRecord

| Field | Type | Rules | Notes |
|---|---|---|---|
| `Scope` | string | required, ≤ `IdentityLength`, part of composite PK | Endpoint/command scope; the **server-resolved** tenant identifier is composed in here (FR-011). Tenant id **may be null** → resolved to a fixed unscoped sentinel so unscoped requests share one partition that never collides with a real tenant |
| `Key` | string | required, ≤ `NameLength`, part of composite PK | Caller-supplied `Idempotency-Key` value |
| `CreatedAt` | DateTimeOffset | required | Existing |
| `State` | RequestState | required | See state machine below |
| `CompletedAt` | DateTimeOffset? | set on complete | Existing |
| `Result` | string? | serialized outcome | Existing; holds the stored command result (see Stored Result) |
| `ProcessedBy` | string? | ≤ `NameLength` | Existing; node identity |
| `RequestHash` | string? | **NEW** — ≤ 128; hash of the original request payload | Enables conflict detection (FR-005) |
| `ExpiresAt` | DateTimeOffset? | **NEW** — indexed | Retention boundary; drives purge (FR-007, FR-009) |
| `LockedAt` | DateTimeOffset? | **NEW** | When processing began; drives in-progress timeout recovery (FR-010) |

**Composite key**: `(Scope, Key)` — unchanged; the unique constraint is the concurrency guard (FR-002, FR-004).

**Indexes**:
- Existing PK on `(Scope, Key)`.
- **NEW** `IX_Idempotency_ExpiresAt` on `ExpiresAt` — used by the purge service.
- **NEW** `IX_Idempotency_State_LockedAt` on `(State, LockedAt)` — used by in-progress timeout recovery.

## Enum: RequestState

Extends `core/src/Juice.Messaging.Idempotency.EF/RequestState.cs`.

| Value | Existing? | Meaning |
|---|---|---|
| `New = 0` | yes | Record created; processing not yet completed (treated as in-progress until `Processed`/`Failed`) |
| `Processed = 1` | yes | Completed successfully; `Result` replayable within retention |
| `Failed = 2` | yes | Not applied (technical failure); retryable — resets to `New` |
| `InProgress = 3` | **NEW** | Explicit in-progress marker with `LockedAt`; recovered to retryable if it exceeds `InFlightTtl` |

### State transitions

```text
(none) --TryBegin/TryCreate--> InProgress   (LockedAt = now, ExpiresAt = now + InFlightTtl)
InProgress --complete(success)--> Processed  (CompletedAt = now, ExpiresAt = now + CompletedRetention, Result set)
InProgress --complete(failure)--> Failed     (retryable)
Failed --TryBegin/TryRetry--> InProgress     (re-execution allowed)
InProgress (LockedAt + InFlightTtl < now) --purge/recover--> Failed  (crashed-node recovery)
any (ExpiresAt < now) --purge--> (deleted)   (retention expiry, EF only)
```

`New = 0` is retained as a legacy value for rows created before this migration and for the
non-HTTP MediatR path that does not set `LockedAt`; reads treat `New` and `InProgress` identically
("in flight"). All **new** records created through `TryBeginRequestAsync` are `InProgress` in a single
insert (no `New → InProgress` double write).

Backward compatibility: existing consumers that only read `New/Processed/Failed` continue to work; `InProgress` is additive and the cache/Redis stores already model in-flight via their 15-minute TTL.

## Value object: IdempotencyOptions (NEW)

Location: `core/src/Juice.Messaging/Idempotency/IdempotencyOptions.cs` (shared by all stores).

| Property | Type | Default | Used by |
|---|---|---|---|
| `InFlightTtl` | TimeSpan | 15 min | Cache/Redis in-flight markers; EF in-progress recovery threshold (FR-010) |
| `CompletedRetention` | TimeSpan | 24 h | Cache/Redis completed TTL; EF `ExpiresAt` on completion (FR-007) |
| `MaxKeyLength` | int | 128 | Key validation (FR-006) |
| `PurgeInterval` | TimeSpan | 5 min | EF purge hosted-service loop (FR-009) |

Replaces the hard-coded `TimeSpan.FromHours(24)` / `TimeSpan.FromMinutes(15)` literals currently in the Redis and DistributedCache stores.

## Stored Result (response replay)

The primary (MediatR) path stores the **command result** in `Result` (existing behavior) and replays it through the same result→HTTP mapping the controller uses, so a replayed request yields an equivalent HTTP response (FR-003). For non-MediatR/minimal-API endpoints, the optional middleware path stores a response envelope:

| Field | Type | Notes |
|---|---|---|
| `StatusCode` | int | Original HTTP status |
| `ContentType` | string | Original content type |
| `Body` | string (serialized) | Original response body |
| `Headers` | map | Whitelisted response headers to replay |

Serialization reuses `IMessageSerializer` (consistent with existing stores).

The MediatR path stores the command result; the minimal-API/middleware path stores the response
**envelope** above. Both are held in the record's `Result` slot and selected by whether the endpoint
routes through a command (`IIdempotentRequest`) or is captured at the response layer (T019a). On
`Outcome=Completed` the stored value is deserialized and replayed as the original HTTP response.

## Request fingerprint (conflict detection)

- On `TryCreate`, compute `RequestHash` from a stable representation of the request payload (method + route + normalized body).
- If a record exists for `(Scope, Key)` with a **different** `RequestHash` → conflict (FR-005) → HTTP 409/422, no execution.
- If the same `RequestHash` → normal replay/in-progress handling.

## Service contract extension (NEW — additive)

`IIdempotencyService` exposes a single begin/complete pair. `TryBeginRequestAsync` returns a rich
outcome and takes an optional fingerprint, serving both the MediatR path and the HTTP path
(the legacy `TryCreateRequestAsync` overloads were removed once all callers migrated):

```csharp
ValueTask<IdempotencyResult> TryBeginRequestAsync(
    string scope, string key, string? requestHash = null,
    CancellationToken cancellationToken = default);

public sealed record IdempotencyResult(IdempotencyOutcome Outcome, string? StoredResult = null);

public enum IdempotencyOutcome
{
    Created,     // caller executes the operation, then calls TryCompleteRequestAsync
    InProgress,  // another execution holds the key, unexpired  -> HTTP 409
    Completed,   // replay StoredResult with the original status -> HTTP 200/201
    Conflict     // same key, different requestHash              -> HTTP 422
}
```

`IdempotencyResult` is a plain contract type in `Juice.Messaging.Idempotency`; the HTTP layer maps
`Outcome` -> status without referencing any concrete store (FR-012). Each of the four stores
(InMemory, DistributedCache, Redis, EF) implements `TryBeginRequestAsync`.

## Validation rules (from requirements)

- `Key` required for opted-in endpoints; empty/missing → 400 (FR-001).
- `Key` length ≤ `MaxKeyLength`, non-whitespace → else 400 (FR-006).
- `(Scope, Key)` uniqueness enforced by composite PK; race → one winner, others get in-progress/replay (FR-002, FR-004).
- `ExpiresAt` in the past → record eligible for purge; a new request with that key is treated as new (FR-007).
