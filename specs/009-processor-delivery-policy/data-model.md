# Data Model: Per-Processor Delivery Policy

**Branch**: `009-processor-delivery-policy` | **Date**: 2026-03-27

> No database changes. This document describes the conceptual in-memory model and how existing types relate to the new feature.

---

## Conceptual Entities

### ProcessorKey

A unique string identifier for one delivery processor instance.

**Format**: `"{publisher}:{contextTypeName}"`
**Example**: `"rabbitmq:OrderDbContext"`, `"local:AuditDbContext"`

Derived at startup from the `publisher` argument and `typeof(TContext).Name` in `AddDeliveryProcessor<TContext>()`. Matches the `PublisherKey` + `Context` fields of `DeliveryContext`.

---

### ProcessorPolicyRegistry (new internal)

Tracks which processor keys have at least one explicitly configured policy.

| Field | Type | Description |
|-------|------|-------------|
| `_keys` | `HashSet<string>` | Set of processor keys with explicit policies |

**Operations**:
- `Register(key)` — called at DI configuration time when `AddDeliveryPolicies` is used on a `DeliveryProcessorBuilder`
- `IsConfigured(key)` — called at policy resolution time

**Lifetime**: Singleton instance stored in `IServiceCollection`. Populated during `ConfigureServices`; read-only at runtime.

---

### DeliveryPolicyOptions (existing — named instances added)

Already exists as a global (unnamed) options registration. This feature adds **named instances** under `ProcessorKey`.

| Field | Type | Processor-level usage |
|-------|------|-----------------------|
| `DefaultPolicy` | `PolicyConfiguration?` | Primary use: the per-processor default policy |
| `Policies` | `Dictionary<string, PolicyConfiguration>` | Optional: per-intent overrides within the processor scope |

**Named instance key**: `ProcessorKey` (e.g. `"rabbitmq:OrderDbContext"`)

---

### PolicyConfiguration (existing — unchanged)

Nullable fields for each delivery tuning parameter. At resolution time, null fields inherit from the next policy in the fallback chain.

| Field | Type | Description |
|-------|------|-----------------------|
| `BatchSize` | `int?` | Records per processing cycle |
| `Interval` | `TimeSpan?` | Delay between cycles |
| `Timeout` | `TimeSpan?` | In-progress timeout before recovery |
| `InitialRetryDelay` | `TimeSpan?` | Delay before first retry |
| `RetryDelayMultiplier` | `double?` | Exponential backoff factor |
| `MaxRetryAttempts` | `int?` | Cap on retry count |

---

### DeliveryPolicy (existing — unchanged)

The fully resolved, non-nullable policy used by `DeliveryWorker`. Result of merging `PolicyConfiguration` layers.

---

## Resolution Chain

```
DeliveryContext(PublisherKey, Intent, Context)
        │
        ▼
Step 1: Global Policies["pub:intent:ctx"] match? ──► yes → return (config-section wins)
        │ no
        ▼
Step 2: Global Policies["pub:intent:*"] match?   ──► yes → return
        │ no
        ▼
Step 3: Global Policies["pub:*:*"] match?        ──► yes → return
        │ no
        ▼
Step 4: Global Policies["*:intent:*"] match?     ──► yes → return
        │ no
        ▼
Step 5: ProcessorPolicyRegistry.IsConfigured("{pub}:{ctx}")?
        │ yes: get named DeliveryPolicyOptions for processor key
        │      if .DefaultPolicy != null
        │        → merge on top of global DefaultPolicy
        │        → return PolicyConfiguration.ToPolicy(global.DefaultPolicy?.ToPolicy())
        │ no / .DefaultPolicy null
        ▼
Step 6: Global DefaultPolicy != null?            ──► yes → return DefaultPolicy.ToPolicy()
        │ no
        ▼
Step 7: DeliveryPolicy.Default (built-in hardcoded values)
```

**Priority tiers**:
| Tier | Source | Overrides |
|------|--------|-----------|
| Highest | Global `Policies` key-matched entries (steps 1–4) | Everything |
| Middle | Processor code `DefaultPolicy` (step 5) | Global `DefaultPolicy` + built-in |
| Lower | Global `DefaultPolicy` (step 6) | Built-in only |
| Lowest | `DeliveryPolicy.Default` (step 7) | — |

**Key invariant**: Steps 1–4 use the global `Policies` dictionary where config-section bindings land. A per-processor appsettings entry (e.g. `"rabbitmq:send-pending:OrderDbContext"`) goes into this dictionary and thus outranks processor code policies at step 5.
