# Quickstart: API Idempotency-Key (Server Side)

**Feature**: `011-idempotency-key-api` | **Date**: 2026-07-12

How a consuming Juice application enables idempotent HTTP endpoints and how a caller uses them.

## 1. Register the idempotency store + HTTP layer

```csharp
// Program.cs / Startup
services.AddMessaging()
    .AddIdempotencyEF(configuration, options =>
    {
        options.DatabaseProvider = "PostgreSQL";   // or "SqlServer"
        options.ConnectionName   = "Messaging";
        options.Schema           = "App";
    });

services.AddApiIdempotency(options =>
{
    options.CompletedRetention = TimeSpan.FromHours(24);   // FR-007
    options.InFlightTtl        = TimeSpan.FromMinutes(15); // FR-010
    options.MaxKeyLength       = 128;                      // FR-006
    options.PurgeInterval      = TimeSpan.FromMinutes(5);  // FR-009
});
```

## 2. Apply migrations (both providers supported)

```bash
dotnet ef database update -c IdempotencyContext \
  -p core/src/Juice.Messaging.Idempotency.EF.PostgreSQL
```

The new migration adds `RequestHash`, `ExpiresAt`, `LockedAt`, the `InProgress` state, and the supporting indexes.

## 3. Mark a state-changing endpoint

```csharp
[HttpPost("orders")]
[Idempotent]                       // requires the Idempotency-Key header
public Task<IActionResult> CreateOrder([FromBody] CreateOrderCommand command)
    => _mediator.Send(command).AsHttp(this);   // dispatched via Juice.MediatR.IMediator
```

`CreateOrderCommand` implements `IIdempotentRequest`; the filter binds the header onto `command.IdempotencyKey`, so the existing `IdempotencyRequestBehavior` enforces exactly-once.

## 4. Call it

```http
POST /orders HTTP/1.1
Idempotency-Key: 5f3e9c2a-1b7d-4a90-9e2c-8a1f0b6d4c11
Content-Type: application/json

{ "sku": "ABC-123", "qty": 2 }
```

- **First call** → order created, `201 Created`, response recorded.
- **Same call retried** (same key + body) → `201 Created` with the **replayed** response, no second order.
- **Same key, different body** → `422 Unprocessable Entity` (key conflict).
- **Same key while still processing** → `409 Conflict` (in progress), retry after the hinted delay.
- **Missing key** → `400 Bad Request` (header required).

## 5. Verify behavior (tests)

Run the feature's tests (infra-dependent tests are `IgnoreOnCIFact`-guarded):

```bash
dotnet test core/test/Juice.AspNetCore.Idempotency.Tests
dotnet test core/test/Juice.Messaging.Idempotency.Tests
```

Key scenarios to assert:
- Concurrent identical requests → one execution, one effect (SC-001, SC-002).
- Retry of a completed key → identical replayed response, no re-execution (SC-003).
- Missing / invalid / conflicting key → distinct rejections, no effect (SC-004).
- Expired records purged within one retention window (SC-006); crashed in-progress record recovered after `InFlightTtl` (FR-010).

## Notes

- The Angular client that generates and attaches the `Idempotency-Key` is a separate feature in `juice-layout` (`002-idempotency-key-api`).
- Cache/Redis stores honor the same `IdempotencyOptions`; only the EF store adds the purge/recovery hosted service.
