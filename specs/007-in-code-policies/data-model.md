# Data Model: In-Code Publishing Policies

## Overview

This feature introduces no new database entities or persistent state. All changes are to in-memory configuration objects that are constructed once at application startup and held as singletons.

---

## Modified Internal Type: PublishRule

**Existing shape** (before this feature):
```
PublishRule
  Priority          : int
  Match             : PublishRuleMatch
  Publishers        : List<PublisherDestination>
```

**New field**:
```
PublishRule
  Priority          : int
  Match             : PublishRuleMatch
  Publishers        : List<PublisherDestination>
  IsCodeDefined     : bool    ← NEW (default: false; set to true by PublishingPolicyBuilder)
```

**Invariants**:
- `IsCodeDefined` is internal — not configurable from JSON and not visible in the public API.
- `Priority` remains an `int`; no range constraints are imposed by the framework.
- A rule with an empty `Match` (all fields null) acts as a wildcard — matches any context.

---

## New Public Type: PublishingPolicyBuilder

**Purpose**: Configuration-time builder that accumulates code-defined policy rules and translates them into `PublishingPolicyOptions` mutations for registration via `IOptions<T>`.

```
PublishingPolicyBuilder
  [internal] _rules     : List<PublishRule>
  [internal] _default   : PublishRule?

  SetDefault(publisherKey, destination, routingKey?) → PublishingPolicyBuilder
  AddRule(priority, Action<PublishRuleBuilder>)       → PublishingPolicyBuilder

  [internal] ApplyTo(PublishingPolicyOptions)         → void
```

**Invariants**:
- `SetDefault` replaces any previously set default within the same builder invocation.
- `AddRule` appends; rules are not deduplicated — the consumer is responsible for priority assignment.
- `ApplyTo` is called once per `services.Configure<PublishingPolicyOptions>` registration; all rules produced by this builder have `IsCodeDefined = true`.

---

## New Internal Type: PublishRuleBuilder

**Purpose**: Fluent builder for a single `PublishRule`. Used inside `PublishingPolicyBuilder.AddRule(...)`.

```
PublishRuleBuilder
  ForEvent(eventTypeName)           → PublishRuleBuilder
  ForDomain(domain)                 → PublishRuleBuilder
  ForTenant(tenantIdentifier)       → PublishRuleBuilder
  ForTenantTier(tenantTier)         → PublishRuleBuilder
  PublishTo(publisherKey,
            destination,
            routingKey?)            → PublishRuleBuilder
```

**Invariants**:
- Calling `ForEvent` / `ForDomain` / `ForTenant` / `ForTenantTier` more than once on the same builder overwrites the previous value (last-write wins).
- Calling `PublishTo` multiple times appends additional `PublisherDestination` entries — one rule can fan out to multiple publishers.
- A `PublishRuleBuilder` with no `ForXxx` calls and no `PublishTo` call produces a wildcard rule with no publishers (routes nothing) — the consumer is responsible for calling `PublishTo` at least once.

---

## Resolution Algorithm (Updated)

`DefaultEventPublishingPolicy.ResolveAsync(context)`:

1. Collect all rules from `PublishingPolicyOptions.Rules`.
2. Sort: `OrderByDescending(Priority).ThenByDescending(IsCodeDefined)`.
3. Find the first rule where `rule.Match.IsMatch(context)` returns `true`.
4. If found → return routes from that rule's `Publishers`.
5. If not found → return routes from `PublishingPolicyOptions.Default.Publishers`.

The `Default` rule contributed by `PublishingPolicyBuilder.SetDefault(...)` is merged into `Options.Default.Publishers` — code-defined defaults are appended to any config-defined defaults (same accumulation mechanism).

> **Note**: If both code and config define a Default, both sets of publishers are active. This is consistent with the existing behavior where `Default` holds a list of publishers (not a single one).
