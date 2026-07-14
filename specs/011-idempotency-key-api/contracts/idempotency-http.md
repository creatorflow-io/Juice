# Contract: HTTP Idempotency-Key Semantics & Public API

**Feature**: `011-idempotency-key-api` | **Date**: 2026-07-12

This feature is a framework library; its "contract" is (a) the HTTP request/response semantics for callers and (b) the public C# API for consuming applications.

## A. HTTP contract (caller-facing)

### Request

| Element | Value |
|---|---|
| Header | `Idempotency-Key: <caller-generated unique value>` |
| Applies to | Endpoints/actions marked `[Idempotent]` (opt-in), typically POST/PUT/PATCH/DELETE |
| Key format | Non-empty, ≤ `MaxKeyLength` (default 128), opaque string (UUID-style recommended) |

### Responses

| Situation | Status | Body | FR |
|---|---|---|---|
| First request, success | Handler's normal status (e.g., 200/201) | Handler's normal body; response recorded | FR-002 |
| Duplicate key, same payload, completed | Original status | Replayed stored result | FR-003 |
| Duplicate key, same payload, still processing | `409 Conflict` (ProblemDetails `title: "Request in progress"`) | Retry-After hint | FR-004 |
| Duplicate key, **different** payload | `422 Unprocessable Entity` (ProblemDetails `title: "Idempotency key conflict"`) | — | FR-005 |
| Required key missing | `400 Bad Request` (ProblemDetails `title: "Idempotency-Key header required"`) | — | FR-001 |
| Invalid key (empty/too long) | `400 Bad Request` (ProblemDetails `title: "Invalid Idempotency-Key"`) | — | FR-006 |
| Prior attempt failed (not applied) | Re-executes as new | Handler's normal response | FR-008 |

ProblemDetails follows RFC 7807, consistent with ASP.NET Core conventions.

### Idempotency guarantees

- Exactly-once effect per `(endpoint scope, key)` within the retention window (default 24h) — FR-002, FR-007.
- After the retention window, the same key is treated as a new operation — FR-007.
- Keys are isolated per tenant — FR-011.

## B. Public C# API (consumer-facing)

### Opt-in marker

```csharp
// Mark a controller action or minimal-API endpoint as requiring idempotency.
[Idempotent]                       // scope defaults to the command/endpoint id
[Idempotent(Scope = "orders")]     // explicit shared scope (optional)
public Task<IActionResult> CreateOrder([FromBody] CreateOrderCommand command) { ... }
```

### Registration (DI)

```csharp
// Enable HTTP idempotency; reuses the configured IIdempotencyService store.
services.AddMessaging()
    .AddIdempotencyEF(configuration, options =>
    {
        options.DatabaseProvider = "PostgreSQL";
        options.ConnectionName = "Messaging";
        options.Schema = "App";
    });

services.AddApiIdempotency(options =>       // NEW
{
    options.CompletedRetention = TimeSpan.FromHours(24);
    options.InFlightTtl        = TimeSpan.FromMinutes(15);
    options.MaxKeyLength       = 128;
    options.PurgeInterval      = TimeSpan.FromMinutes(5);
});
```

`AddApiIdempotency` registers:
- the action/endpoint filter that reads and validates `Idempotency-Key`,
- `IdempotencyOptions` binding,
- the EF purge/recovery hosted service (`IdempotencyPurgeHostedService`) when the EF store is active.

### Options

`IdempotencyOptions { InFlightTtl, CompletedRetention, MaxKeyLength, PurgeInterval }` — see [data-model.md](../data-model.md). These replace the previously hard-coded TTLs in the Redis and DistributedCache stores.

### Reused and extended contracts

- `IIdempotentRequest { string IdempotencyKey }` — **unchanged**; commands opting into the MediatR path implement this; the filter binds the header value onto it.
- `IIdempotencyService.TryCompleteRequestAsync(scope, key, success, result)` — **unchanged**.
- `IIdempotencyService.TryBeginRequestAsync(scope, key, requestHash)` → `IdempotencyResult { Outcome, StoredResult }` — the single begin entry point for **both** the HTTP filter and the MediatR path. Distinguishes Created / InProgress / Completed / Conflict in one round-trip, then maps `Outcome` → status (400/409/422/replay). The legacy `TryCreateRequestAsync` overloads were removed once all callers migrated. See [data-model.md](../data-model.md) "Service contract extension".

## C. Backward compatibility

- Existing MediatR consumers of `IIdempotencyService` are unaffected (new fields are nullable/additive; `InProgress` state is additive).
- EF schema changes ship as new migrations for **both** SQL Server and PostgreSQL (FR-014); no data backfill required (existing rows get `NULL` `RequestHash`/`ExpiresAt` and are purged lazily or on next touch).
