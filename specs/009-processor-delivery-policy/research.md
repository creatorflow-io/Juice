# Research: Per-Processor Delivery Policy

**Branch**: `009-processor-delivery-policy` | **Date**: 2026-03-27

All unknowns resolved from in-repo code — no external research required.

---

## Decision 1: API surface on `DeliveryProcessorBuilder`

**Decision**: Add two new methods mirroring the existing `DeliveryBuilder.AddDeliveryPolicies` overloads:
- `AddDeliveryPolicies(IConfigurationSection)`
- `AddDeliveryPolicies(Action<DeliveryPolicyOptions>)`

**Rationale**: Consistency with the existing global API means developers face no new learning curve. The `IConfigurationSection` overload supports appsettings-driven config; the delegate overload supports code-first config. Both are stored in the builder as a list and applied in `RegisterProcessorPolicies<TContext>()` when `TContext` is finally known.

**Alternatives considered**:
- `AddDeliveryPolicies(Action<PolicyConfiguration>)` (configure only the `DefaultPolicy` field) — rejected because it would be inconsistent with the existing API and would prevent users from also defining per-intent policies within the processor scope.
- Making `DeliveryProcessorBuilder<TContext>` generic — rejected because it is a breaking API change and gains nothing over the deferred-registration approach.

---

## Decision 2: Storage mechanism for processor-scoped policies

**Decision**: Use ASP.NET Core **named options** — `services.Configure<DeliveryPolicyOptions>(processorKey, ...)` — with processor key `"{publisher}:{typeof(TContext).Name}"`.

**Rationale**: Named options are the idiomatic .NET mechanism for scoped configuration. `IOptionsMonitor<DeliveryPolicyOptions>.Get(key)` retrieves them at resolution time without any additional infrastructure. The processor key matches the two fields already present in `DeliveryContext` (`PublisherKey` + `Context`).

**Alternatives considered**:
- A custom dictionary of `DeliveryPolicyOptions` keyed by processor key stored as a singleton — rejected as reinventing named options.
- Keyed DI (`AddKeyedSingleton<DeliveryPolicyOptions>(processorKey)`) — rejected because it cannot be configured incrementally via `Configure<T>` (no `TryConfigureKeyedSingleton`).

---

## Decision 3: Tracking which processor keys have explicit policies

**Decision**: Introduce an internal `ProcessorPolicyRegistry` class — a `HashSet<string>` wrapper registered as a singleton instance in `IServiceCollection` (same pattern as `DeliveryProcessorRegistry`).

**Rationale**: `IOptionsMonitor<T>.Get(name)` always returns a value (never null) — an unconfigured named options entry returns a default-constructed `DeliveryPolicyOptions` with all-null fields. Without a separate registry, there is no way to distinguish "explicitly configured" from "never touched". The registry is populated at DI-configuration time (startup) and is fully populated before any worker resolves its policy.

**Alternatives considered**:
- Checking if all fields of the returned `DeliveryPolicyOptions` are null/empty — fragile; silently ignores explicit zero/empty values.
- A `bool` property on the options — not possible without modifying `DeliveryPolicyOptions` and coupling it to the processor concept.

---

## Decision 4: Integration point for processor-scoped resolution

**Decision**: Enhance `DeliveryPolicyConfiguration` (the existing `IDeliveryPolicyProvider`) to insert the processor code policy check **after** the existing four key-match lookups and **before** the global `DefaultPolicy` fallback.

Updated `GetPolicyAsync` resolution chain (7 steps):
1. Global `Policies` exact key `"publisher:intent:context"` → return (includes config-section entries)
2. Global `Policies` wildcard `"publisher:intent:*"` → return
3. Global `Policies` wildcard `"publisher:*:*"` → return
4. Global `Policies` wildcard `"*:intent:*"` → return
5. **[NEW]** Processor code policy: if `ProcessorPolicyRegistry.IsConfigured("{PublisherKey}:{Context}")` → get named `DeliveryPolicyOptions` for processor key → if `DefaultPolicy != null` → merge on top of global `DefaultPolicy` via `PolicyConfiguration.ToPolicy(globalDefaultPolicy)` → return
6. Global `DefaultPolicy` → return
7. `DeliveryPolicy.Default` (built-in) → return

**Key invariant from `/speckit.clarify`**: Global config-section entries in the `Policies` dictionary (steps 1–4) have **higher** priority than processor code policies (step 5). Only the global `DefaultPolicy` (step 6) is lower. This allows operators to override processor code defaults via appsettings without code changes.

**Rationale**: Single provider handles all levels — no IEnumerable ordering concerns. Processor code policy merges on top of global `DefaultPolicy` (not the key-matched result), so partial overrides inherit correctly from the global default while key-matched entries still win outright.

**Optional injection via `IEnumerable<ProcessorPolicyRegistry>`**: `ProcessorPolicyRegistry` is registered only when at least one processor has explicit policies. `IEnumerable<T>` injection returns an empty collection when no instances are registered, making the constructor safe without conditional registration.

**Alternatives considered**:
- Registering a second `IDeliveryPolicyProvider` that runs before `DeliveryPolicyConfiguration` — rejected because `TryAddEnumerable` does not guarantee insertion position relative to existing registrations; ordering would be brittle.
- Checking processor at the very start (before key-match lookups) — rejected after `/speckit.clarify` confirmed that global config-section key entries MUST outrank processor code policies.

---

## Decision 5: `DeliveryProcessorBuilder` — where `RegisterProcessorPolicies<TContext>` is called

**Decision**: Called from `DeliveryBuilder.AddDeliveryProcessor<TContext>()` immediately after `configure?.Invoke(builder)`, before `_services.AddHostedService(...)`.

**Rationale**: At this point `TContext` is known (can compute processor key), the configure delegate has run (policies have been accumulated in the builder), and the service collection is still open. This is the natural place to flush accumulated configuration into the service collection.

---

## Summary of New/Changed Files

| File | Change |
|------|--------|
| `Processing/DeliveryProcessorBuilder.cs` | Add `AddDeliveryPolicies` (×2) + `RegisterProcessorPolicies<TContext>()` |
| `DeliveryBuilder.cs` | Call `procBuilder.RegisterProcessorPolicies<TContext>()` in both `AddDeliveryProcessor` overloads |
| `Internal/DeliveryPolicyConfiguration.cs` | Inject `ProcessorPolicyRegistry?` + `IOptionsMonitor<DeliveryPolicyOptions>?`; add processor-scope check at top of `GetPolicyAsync` |
| `Internal/ProcessorPolicyRegistry.cs` | New: `HashSet<string>` wrapper, singleton-instance pattern |
| `core/test/Juice.EventBus.Tests/DeliveryPoliciesTest.cs` | Add 3 tests: processor policy, fallback, partial override |
