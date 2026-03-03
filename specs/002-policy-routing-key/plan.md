# Implementation Plan: Policy-Controlled Routing Key

**Branch**: `002-policy-routing-key` | **Date**: 2026-03-03 | **Spec**: [spec.md](spec.md)
**Input**: Feature specification from `/specs/002-policy-routing-key/spec.md`

## Summary

Allow `IMessagePublishingPolicy` to specify a per-route routing key that overrides the default event-name-derived routing key used by transport publishers. The policy-configured key is carried via a new `x-routing-key` message header, which is set by `CompositeEventPublisher` and consumed first by `RabbitMQProducer`. Because headers are persisted with the outbox event, this design covers both the direct-publish path and the outbox-delivery path without any database schema changes.

## Technical Context

**Language/Version**: C# on .NET 6, 8, 9 (multi-targeted)
**Primary Dependencies**: `Juice.Messaging`, `Juice.EventBus`, `Juice.EventBus.RabbitMQ`, `Microsoft.Extensions.Options`
**Storage**: No storage changes — routing key travels as a message header, persisted in the existing outbox `Headers` JSON column
**Testing**: xUnit + FluentAssertions (existing `Juice.EventBus.Tests`, `PublishPoliciesTest`)
**Target Platform**: Library (NuGet packages under `core/src/`)
**Performance Goals**: No impact — one extra header null-check per publish
**Constraints**: Strictly additive — no breaking changes to public types; no DB migrations
**Scale/Scope**: 5 files modified, ~3 new unit tests added to `PublishPoliciesTest.cs`

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Status | Notes |
|-----------|--------|-------|
| I. Lightweight & Dual-Architecture | ✅ Pass | Additive only; no new abstractions without concrete usage; no hosting model constraint added |
| II. Library-First Composability | ✅ Pass | Changes stay within existing libraries; layering preserved (`Messaging` < `EventBus` < `EventBus.RabbitMQ`); no new library needed; no circular dep introduced |
| III. DDD + CQRS | ✅ Pass | Not affected — this is messaging infrastructure; no domain model changes |
| IV. Reliable Messaging via Outbox | ✅ Pass | `IMessagePublishingPolicy` remains the sole routing authority; routing key stored in headers, persisted atomically with outbox record; delivery path unchanged; no bypass of outbox |
| V. Multi-Tenancy First | ✅ Pass | `PolicyResolveContext` already includes tenant info; custom policies can incorporate tenant context in routing keys |

**Post-design re-check**: ✅ All principles still pass — header-based approach confirmed in research.md.

## Project Structure

### Documentation (this feature)

```text
specs/002-policy-routing-key/
├── plan.md              # This file
├── research.md          # Phase 0 output ✅
├── data-model.md        # Phase 1 output ✅
├── quickstart.md        # Phase 1 output ✅
├── contracts/
│   └── public-api.md    # Phase 1 output ✅
└── tasks.md             # Phase 2 output (/speckit.tasks — not yet created)
```

### Source Code (affected files only)

```text
core/src/Juice.Messaging/
├── Policies/
│   ├── PublishRoute.cs                           # Add RoutingKey? = null (3rd optional param)
│   └── Internal/
│       ├── PublishingPolicyOptions.cs            # Add RoutingKey? to PublisherDestination
│       └── DefaultEventPublishingPolicy.cs       # Propagate RoutingKey in Map()

core/src/Juice.EventBus/
└── Internal/
    └── CompositeEventPublisher.cs                # Set x-routing-key header from route

core/src/Juice.EventBus.RabbitMQ/
└── Publishing/
    └── RabbitMQProducer.cs                       # Check x-routing-key header first

core/test/Juice.EventBus.Tests/
└── PublishPoliciesTest.cs                        # New routing-key tests
```

**Structure Decision**: Single-library modification pattern — no new projects, no new solution entries. All changes are in existing `core/src/` libraries and their corresponding `core/test/` project.

## Phase 0: Research (complete)

See [research.md](research.md) for all decisions and rationale.

Key decisions:
- **Header-based transport**: Routing key travels as `x-routing-key` message header — avoids DB migration, covers both direct and outbox paths automatically.
- **`PublishRoute` extension**: Optional 3rd positional parameter `string? RoutingKey = null` — backward-compatible at all callsites.
- **`RabbitMQProducer` chain**: `x-routing-key` → `x-message-name` → `x-message-type` → error.
- **MINOR version change only**: No breaking changes.

## Phase 1: Design & Contracts (complete)

See:
- [data-model.md](data-model.md) — extended value types and header specification
- [contracts/public-api.md](contracts/public-api.md) — `PublishRoute` API contract and configuration schema
- [quickstart.md](quickstart.md) — end-to-end usage examples

## Implementation Design

### Change 1: `PublishRoute.cs`

```csharp
// Before
public sealed record PublishRoute(string PublisherKey, string Destination);

// After
public sealed record PublishRoute(
    string PublisherKey,
    string Destination,
    string? RoutingKey = null);
```

### Change 2: `PublishingPolicyOptions.cs`

```csharp
// Add to PublisherDestination:
public string? RoutingKey { get; init; }
```

### Change 3: `DefaultEventPublishingPolicy.cs`

```csharp
// Before
IReadOnlyCollection<PublishRoute> routes = [..
    publishers.Select(p => new PublishRoute(p.Key, p.Destination))];

// After
IReadOnlyCollection<PublishRoute> routes = [..
    publishers.Select(p => new PublishRoute(p.Key, p.Destination, p.RoutingKey))];
```

### Change 4: `CompositeEventPublisher.cs`

In `PublishAsync<T>`, after building the `headers` dictionary, add:

```csharp
// Add x-routing-key when policy specifies one
if (!string.IsNullOrEmpty(route.RoutingKey))
{
    headers["x-routing-key"] = route.RoutingKey;
}
```

This requires threading `route` into the inner `PublishAsync` call. Currently the route is decomposed into `PublisherKey` + `Destination` at the call site. The routing key needs to travel alongside.

**Implementation approach**: Add the header before calling `PublishAsync` by passing the routing key into the existing headers dict at the foreach loop where routes are iterated:

```csharp
foreach (var route in routes)
{
    var publishContext = new PublishContext(@event.MessageId.ToString())
    {
        Destination = route.Destination,
        TenantId = _tenantAccessor?.Tenant?.Id
    };
    await PublishAsync(@event, route.PublisherKey, publishContext, ctx, ct,
        routingKey: route.RoutingKey);
}
```

Or more simply: set `x-routing-key` in the headers dict inside `PublishAsync` by passing the routing key:

```csharp
// In the outer PublishAsync loop, before calling the private overload:
var extraHeaders = !string.IsNullOrEmpty(route.RoutingKey)
    ? new Dictionary<string, object?> { ["x-routing-key"] = route.RoutingKey }
    : null;
```

The cleanest approach given the existing code structure is to add `x-routing-key` directly into the headers dictionary that `CompositeEventPublisher` builds in `PublishAsync<T>` (the private method), requiring the routing key to be passed down. See tasks for the exact approach.

### Change 5: `RabbitMQProducer.cs`

```csharp
// Before
var routingKey = headers.GetHeaderString("x-message-name")
    ?? headers.GetHeaderString("x-message-type")
    ?? throw new InvalidOperationException(...);

// After
var routingKey = headers.GetHeaderString("x-routing-key")
    ?? headers.GetHeaderString("x-message-name")
    ?? headers.GetHeaderString("x-message-type")
    ?? throw new InvalidOperationException(...);
```

### New Tests in `PublishPoliciesTest.cs`

Three new `[Fact]` tests:
1. `Should_Include_RoutingKey_In_Resolved_Route_When_Configured` — verifies `PublishRoute.RoutingKey` is populated from `PublisherDestination.RoutingKey`.
2. `Should_Return_Null_RoutingKey_When_Not_Configured` — verifies backward compat: omitting `RoutingKey` in config yields `null` on the route.
3. `Should_Include_RoutingKey_For_Matching_Rule_Only` — two rules, only one has a routing key; verify each resolves correctly.

## Complexity Tracking

No violations to justify. All changes are minimal, additive, and follow established patterns.
