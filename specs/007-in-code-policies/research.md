# Research: In-Code Publishing Policies

## Decision 1: Merge Strategy for Code + Config Rules

**Decision**: Use .NET's `IOptions<T>` accumulation — code rules are registered via an additional `services.Configure<PublishingPolicyOptions>(action)` call. Both sources contribute to the same options object automatically.

**Rationale**: `IOptions<T>` in .NET accumulates all `Configure<T>` delegates in registration order. The existing `DefaultEventPublishingPolicy` already resolves `IOptions<PublishingPolicyOptions>` at runtime; no changes to the resolver are needed to pick up code-defined rules. This is the idiomatic .NET pattern (same technique used by ASP.NET Core to layer middleware options, Kestrel config, etc.).

**Alternatives considered**:
- Introduce a separate `ICodePublishingPolicyOptions` and a composite `DefaultEventPublishingPolicy` that reads from both — rejected; two options types add indirection with no benefit since .NET options already handles accumulation natively.
- Build an entirely separate `IMessagePublishingPolicy` implementation for code rules and compose the two policies — rejected; adds an extra service registration and a composition class for no gain.

---

## Decision 2: Tiebreaker When Code and Config Rules Share the Same Priority

**Decision**: Add a `bool IsCodeDefined` flag to the internal `PublishRule` record. The resolution sort becomes `OrderByDescending(priority).ThenByDescending(isCodeDefined)`, so code rules win when priorities are equal.

**Rationale**: The spec requires code rules take precedence at equal priority. A boolean flag on the rule is the minimal change — no new type, no extra DI registration. It is fully internal and invisible to consuming applications.

**Alternatives considered**:
- Automatically bump code rule priorities by a small constant (e.g., +0.001) — rejected; requires changing priority to `decimal`, breaking the existing `int` API.
- Process code rules last and reverse the match logic — rejected; confusing and fragile.

---

## Decision 3: Public API Surface (PublishingPolicyBuilder)

**Decision**: Introduce a `PublishingPolicyBuilder` class (public, concrete, no interface — consistent with `DeliveryBuilder`, `DeliveryProcessorBuilder`). It exposes:
- `SetDefault(publisherKey, destination, routingKey?)` — sets the `Default` rule
- `AddRule(priority, Action<PublishRuleBuilder>)` — appends a rule with match criteria + publishers
- A `PublishRuleBuilder` inner type with `ForEvent`, `ForDomain`, `ForTenant`, `ForTenantTier`, `PublishTo` chainable methods

The builder accumulates rules and translates them into `PublishingPolicyOptions` mutations, which are then registered via `services.Configure<PublishingPolicyOptions>`.

**Rationale**: Mirror the existing `DeliveryPolicyBuilder` / `DeliveryProcessorBuilder` style. Public concrete builder (no interface) because the builder is a configuration-time artifact, not a runtime service. The public field names (`ForEvent`, `ForDomain`) mirror the JSON schema (`Event`, `Domain`) to keep the mental model consistent between code and config.

**Alternatives considered**:
- Expose `PublishingPolicyOptions` directly as a public type — rejected; would force making `PublisherDestination`, `PublishRule`, `PublishRuleMatch` public too, enlarging the public API surface unnecessarily.
- Accept `Action<PublishingPolicyOptions>` directly — rejected; `PublishingPolicyOptions` is `internal`, so consuming applications cannot use this signature without a public wrapper.

---

## Decision 4: Custom Policy Registration

**Decision**: Add a `AddPublishingPolicies<TPolicy>()` overload to `MessagingBuilder` that registers `TPolicy` as `IMessagePublishingPolicy` (singleton, `TryAddSingleton`). The existing guard (skip if already registered) is preserved — calling the config or code-based overload after a custom policy has been registered is a no-op.

**Rationale**: The existing guard already provides the correct behavior: the first `IMessagePublishingPolicy` registration wins. A dedicated generic overload makes custom registration explicit and discoverable rather than requiring consumers to call `services.AddSingleton<IMessagePublishingPolicy, TPolicy>()` directly.

**Alternatives considered**:
- Force-replace any existing registration — rejected; violates least-surprise and could silently override a valid earlier registration.
- Register custom policies as a decorator around `DefaultEventPublishingPolicy` — out of scope per spec assumptions; the spec treats custom policy registration as a standalone alternative, not a composite.

---

## Decision 5: Affected Files (Minimal-Change Set)

All changes are within `core/src/Juice.Messaging/` (single library project — no new project required):

| File | Change |
|------|--------|
| `Policies/Internal/PublishingPolicyOptions.cs` | Add `bool IsCodeDefined` to `PublishRule` |
| `Policies/Internal/DefaultEventPublishingPolicy.cs` | Add `ThenByDescending(r => r.IsCodeDefined)` tiebreaker |
| `Policies/PublishingPolicyBuilder.cs` | New file — public builder + `PublishRuleBuilder` |
| `MessagingBuilder.cs` | Add two new `AddPublishingPolicies` overloads |
| `core/test/Juice.Messaging.Tests/` | Add `PublishingPolicyBuilderTest.cs` — unit tests, no infra needed |

No migration, no new NuGet package, no breaking API changes.
