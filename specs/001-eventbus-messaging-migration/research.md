# Research: EventBus Contracts → Messaging Contracts Migration

**Branch**: `001-eventbus-messaging-migration` | **Date**: 2026-02-24

## Current State Analysis

### Existing Package: `Juice.EventBus.Contracts`

Location: `core/src/Juice.EventBus.Contracts/`
Target frameworks: `net6.0;net8.0;net9.0` (via `$(AppTargetFramework)`)
Root namespace: `Juice.EventBus`

**Public types (3 files, ~50 LOC total):**

| Type | Kind | Namespace | Inherits/Implements |
|------|------|-----------|---------------------|
| `IIntegrationEvent` | interface | `Juice.EventBus` | `IEvent` (from `Juice.Contracts`) |
| `IntegrationEvent` | abstract record | `Juice.EventBus` | `MessageBase`, `IIntegrationEvent` |
| `IIntegrationEventHandler<T>` | interface | `Juice.EventBus` | — |

**Packages that reference `Juice.EventBus.Contracts` (direct):**

| Package | Reference type | Action needed |
|---------|---------------|---------------|
| `Juice.EventBus` | ProjectReference | Update to `Juice.Messaging.Contracts` |
| `Juice.EF.Tests.Shared` (test) | ProjectReference | Update to `Juice.Messaging.Contracts` |

**C# files using `Juice.EventBus` namespace (indirect — via `Juice.EventBus` package):**
- `Juice.EventBus` project: ~6 files (IEventBus, dispatcher, publishers, DI extensions)
- `Juice.EventBus.RabbitMQ` project: ~8 files (producer, consumer, DI)
- `Juice.Messaging.Outbox.Delivery` project: DeliveryWorker.cs, DeliveryProcessor.cs
- Test projects: ~15 files (event definitions, handler implementations, test classes)

### Placeholder Already Exists

`core/src/Juice.Messaging.Contracts/` directory exists but is **empty** — it was reserved as
a placeholder for exactly this migration. No `.csproj` or `.cs` files exist yet.

---

## Research Decisions

### Decision 1: New Package Location and Name

- **Decision**: Create the new project at `core/src/Juice.Messaging.Contracts/`
  (using the existing empty placeholder directory).
- **Package name**: `Juice.Messaging.Contracts`
- **Namespace**: `Juice.Messaging`
- **Rationale**: The directory already exists as a deliberate placeholder. The `Juice.Messaging`
  namespace is already established in the framework (`Juice.Messaging`, `Juice.Messaging.Outbox`,
  etc.). Placing integration event contracts there is architecturally correct — they are
  messaging primitives, not EventBus-specific concepts.
- **Alternatives considered**:
  - `Juice.Integration.Contracts` — rejected; "Integration" is too vague and not a first-class
    namespace in the existing framework.
  - Keep in `Juice.EventBus.Contracts` under a new sub-namespace — rejected; this defeats the
    goal of decoupling the contracts from the EventBus implementation label.

### Decision 2: Target Framework

- **Decision**: Use `$(AppTargetFramework)` (`net6.0;net8.0;net9.0`), matching the existing
  `Juice.EventBus.Contracts` project.
- **Rationale**: The existing contracts project targets app frameworks (not `netstandard2.1`),
  ensuring maximum compatibility with framework-version-specific features and consistent
  versioning with the rest of the Juice suite.
- **Alternatives considered**:
  - `netstandard2.1` — rejected; would diverge from the existing contracts project's target
    and could limit future use of framework-specific APIs.

### Decision 3: Backward Compatibility Mechanism

- **Decision**: **Subtype inheritance approach** — the old `Juice.EventBus` types in
  `Juice.EventBus.Contracts` are updated to extend/implement the new `Juice.Messaging`
  canonical types from `Juice.Messaging.Contracts`. The old types remain visible at compile
  time; the new types become their supertypes.

  Specifically:
  - `Juice.EventBus.IIntegrationEvent` → `interface IIntegrationEvent : Juice.Messaging.IIntegrationEvent { }` (extends canonical)
  - `Juice.EventBus.IntegrationEvent` → `abstract record IntegrationEvent : Juice.Messaging.IntegrationEvent` (inherits canonical)
  - `Juice.EventBus.IIntegrationEventHandler<T>` → remains but marked `[Obsolete]`; internal
    Juice packages switch to `Juice.Messaging.IIntegrationEventHandler<T>` for handler resolution.

- **Rationale**: This is the only mechanism that achieves both compile-time backward
  compatibility (old namespace names still resolve) and runtime compatibility (old types are
  subtypes of new types, enabling upcast to the canonical interface). Spec FR-004 requires
  runtime compatibility; the subtype relationship achieves this for all interface usages.

- **CLR identity note**: True CLR identity (exact same type at two namespaces) is not possible
  in .NET — `[assembly: TypeForwardedTo]` only works with the same namespace, and C# type
  aliases (`using X = Y`) are not exported to consuming assemblies. The subtype approach is
  the industry-standard solution for namespace migrations (used by Microsoft for multiple
  namespace reorganisations in .NET).

- **Alternatives considered**:
  - `[assembly: TypeForwardedTo]` — rejected; only works when the namespace is unchanged;
    would require keeping types in `Juice.EventBus` namespace permanently.
  - C# 12 `global using` aliases — rejected; aliases defined in a library project are NOT
    propagated to consuming projects; consuming code would still see `Juice.EventBus` types
    as unrelated to `Juice.Messaging` types at runtime.
  - Parallel independent types with no relationship — rejected; breaks handler dispatch
    (reflection-based `typeof(IIntegrationEventHandler<>).MakeGenericType(...)` must find
    handlers regardless of which namespace the consumer used).

### Decision 4: Deprecation Signal

- **Decision**: Mark old types with `[Obsolete("Use Juice.Messaging.X instead. See migration guide.", false)]`
  (warning, not error) to surface deprecation as a compiler warning (CS0618) without breaking
  existing builds. Also update the `Juice.EventBus.Contracts.csproj` `<Description>` to include
  a deprecation notice.
- **Rationale**: `false` (warning, not error) ensures existing consumers are notified without
  requiring immediate action. The package description makes deprecation visible in NuGet UI.
- **Alternatives considered**:
  - `[Obsolete(..., true)]` (error) — rejected; violates SC-001 which requires zero compilation
    errors after upgrading without source changes.

### Decision 5: Handler Dispatch Compatibility

- **Decision**: The `IntegrationEventDispatcher` (in `Juice.EventBus`) is updated to resolve
  handlers using `Juice.Messaging.IIntegrationEventHandler<T>` as the primary resolution type.
  Since old handlers implementing `Juice.EventBus.IIntegrationEventHandler<T>` (which extends
  `Juice.Messaging.IIntegrationEventHandler<T>`) satisfy the interface constraint, they will
  be found and invoked correctly.
- **Rationale**: Changing the resolution type to the canonical `Juice.Messaging` interface is
  required for new consumers who implement `Juice.Messaging.IIntegrationEventHandler<T>` to
  be found at runtime. Old handlers remain compatible via inheritance.

### Decision 6: Internal Package Migration Scope

The following packages reference `Juice.EventBus.Contracts` and MUST be updated to reference
`Juice.Messaging.Contracts` directly:

| Package | Change |
|---------|--------|
| `Juice.EventBus` | Replace `Juice.EventBus.Contracts` reference with `Juice.Messaging.Contracts` |
| `Juice.EF.Tests.Shared` | Replace `Juice.EventBus.Contracts` reference with `Juice.Messaging.Contracts` |

`Juice.EventBus.Contracts` itself adds a reference to `Juice.Messaging.Contracts` (for the
subtype hierarchy). All other packages that currently use `Juice.EventBus` namespace types do
so transitively through `Juice.EventBus` package — they do not need .csproj changes, only
(optionally) a `using` statement update if they wish to migrate to the new namespace.

### Decision 7: Test Strategy

- **Decision**: Update test event classes in `Juice.EF.Tests.Shared` to use
  `Juice.Messaging.IntegrationEvent` as their base class. Existing test files in
  `Juice.EventBus.Tests` and `Juice.Integrations.Tests` require NO changes (they use the
  old namespace transitively and the subtype relationship ensures correct dispatch).
- **Rationale**: Test shared helpers should demonstrate canonical usage. Framework test files
  that exercise existing behavior must pass without modification per SC-004.

---

## Implementation Approach Summary

```
Before:
  Juice.Contracts → Juice.EventBus.Contracts (Juice.EventBus namespace)
                          ↓
                    Juice.EventBus ← Juice.EventBus.RabbitMQ

After:
  Juice.Contracts → Juice.Messaging.Contracts (Juice.Messaging namespace) ← [NEW canonical]
                          ↓ (also referenced by)
                    Juice.EventBus.Contracts (Juice.EventBus namespace, deprecated shim)
                          ↓ (plus direct reference)
                    Juice.EventBus ← Juice.EventBus.RabbitMQ
```

**Dependency changes:**
1. `Juice.Messaging.Contracts` is created with canonical types in `Juice.Messaging` namespace
2. `Juice.EventBus.Contracts` references `Juice.Messaging.Contracts` and re-exports subtypes
3. `Juice.EventBus.csproj` swaps its reference from `Juice.EventBus.Contracts` to `Juice.Messaging.Contracts`
4. `Juice.EF.Tests.Shared.csproj` swaps its reference similarly
5. `Juice.EventBus.Contracts.csproj` adds `Juice.Messaging.Contracts` reference (for subtype defs)
