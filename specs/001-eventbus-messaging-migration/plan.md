# Implementation Plan: EventBus Contracts → Messaging Contracts Migration

**Branch**: `001-eventbus-messaging-migration` | **Date**: 2026-02-24 | **Spec**: [spec.md](./spec.md)
**Input**: Feature specification from `/specs/001-eventbus-messaging-migration/spec.md`

## Summary

Move integration event contracts (`IIntegrationEvent`, `IntegrationEvent`,
`IIntegrationEventHandler<T>`) from the `Juice.EventBus` namespace (in
`Juice.EventBus.Contracts`) to the `Juice.Messaging` namespace in a new canonical
`Juice.Messaging.Contracts` library. The old `Juice.EventBus.Contracts` package is
retained as a deprecated shim whose types extend the canonical types, ensuring
existing consumers compile and run without any source-code changes.

## Technical Context

**Language/Version**: C# (LangVersion: latest) on net6.0 / net8.0 / net9.0
**Primary Dependencies**: `Juice.Contracts` (foundation types: IEvent, IMessage, MessageBase)
**Storage**: N/A — pure contract library, no persistence
**Testing**: xUnit 2.9 (`dotnet test`); existing integration tests in `Juice.Integrations.Tests`
**Target Platform**: .NET 6 / 8 / 9 multi-target (`$(AppTargetFramework)`)
**Project Type**: NuGet library (two packages: new canonical + updated deprecated shim)
**Performance Goals**: N/A — no runtime hot path changes (contracts only)
**Constraints**: Zero compilation errors on existing consumer code after upgrade (SC-001);
all existing framework tests pass without modification (SC-004)
**Scale/Scope**: 3 source files created; 2 `.csproj` files updated; 2 `.cs` files in shim
updated; internal dispatcher updated; 2 test event files updated

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-checked after Phase 1 design.*

| Principle | Status | Notes |
|-----------|--------|-------|
| I. Lightweight & Dual-Architecture | ✅ PASS | Pure contract refactor; no new runtime dependencies or hosting constraints |
| II. Library-First Composability | ✅ PASS | New `Juice.Messaging.Contracts` is a standalone, independently publishable library with a clear abstraction boundary; placed in `core/src/`; dependency direction preserved (Contracts layer) |
| III. Domain-Driven Design + CQRS | ✅ PASS | No changes to domain model, behavior pipeline, or mediator; not applicable to this contract library |
| IV. Reliable Messaging via Outbox | ✅ PASS | Outbox pipeline unchanged; `MessageContext`, `DeliveryHostedService`, `IMessagePublishingPolicy` all unaffected |
| V. Multi-Tenancy First | ✅ PASS | `TenantId` on `MessageBase` (inherited by `IntegrationEvent`) is preserved in the canonical type |

**Governance compliance**: No breaking change to external public API (deprecated shim preserves old types). MAJOR version bump is NOT required — old types remain. This is a MINOR addition (new package). Deprecation warning (CS0618) is non-breaking by constitution definition.

**Post-design re-check**: ✅ All principles satisfied. The subtype hierarchy (old types extend new canonical types) preserves the layering order: `Juice.Contracts → Juice.Messaging.Contracts → Juice.EventBus.Contracts`. No circular dependencies introduced.

## Project Structure

### Documentation (this feature)

```text
specs/001-eventbus-messaging-migration/
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output
├── quickstart.md        # Phase 1 output
├── contracts/
│   └── public-api.md    # Phase 1 output
└── tasks.md             # Phase 2 output (/speckit.tasks — not yet created)
```

### Source Code Changes

```text
core/src/
├── Juice.Messaging.Contracts/          ← NEW (empty directory already exists)
│   ├── Juice.Messaging.Contracts.csproj
│   ├── IIntegrationEvent.cs            ← canonical type in Juice.Messaging namespace
│   ├── IntegrationEvent.cs             ← canonical type in Juice.Messaging namespace
│   └── IIntegrationEventHandler.cs     ← canonical type in Juice.Messaging namespace
│
├── Juice.EventBus.Contracts/           ← MODIFIED (deprecated shim)
│   ├── Juice.EventBus.Contracts.csproj ← add Juice.Messaging.Contracts reference + deprecation description
│   ├── IIntegrationEvent.cs            ← extend Juice.Messaging.IIntegrationEvent + [Obsolete]
│   ├── IntegrationEvent.cs             ← extend Juice.Messaging.IntegrationEvent + [Obsolete]
│   └── IIntegrationEventHandler.cs     ← add [Obsolete] attribute
│
└── Juice.EventBus/                     ← MODIFIED (internal consumer)
    ├── Juice.EventBus.csproj           ← swap Contracts reference to Juice.Messaging.Contracts
    ├── Dispatching/
    │   └── IntegrationEventDispatcher.cs ← update handler resolution type to Juice.Messaging namespace
    └── *.cs (using statements)         ← update Juice.EventBus → Juice.Messaging for contract types

core/test/
└── Juice.EF.Tests.Shared/             ← MODIFIED (demonstrates canonical usage)
    ├── Juice.EF.Tests.Shared.csproj   ← swap to Juice.Messaging.Contracts reference
    └── Events/
        ├── ContentPublishedIntegrationEvent.cs    ← update using + base class to Juice.Messaging
        └── ContentNameChangedIntegrationEvent.cs  ← update using + base class to Juice.Messaging
```

**Structure Decision**: Library / NuGet Package layout. Source in `core/src/`, tests in
`core/test/`. The new `Juice.Messaging.Contracts` project uses the pre-existing empty
placeholder directory at `core/src/Juice.Messaging.Contracts/`. No new test project is created
for the contracts library itself — the existing integration tests in `Juice.Integrations.Tests`
and `Juice.EventBus.Tests` (which exercise the full event dispatch pipeline) serve as the
acceptance test suite per SC-004.

## Complexity Tracking

> No Constitution violations requiring justification. All design choices are within established patterns.

---

## Phase 0: Research

*Status: ✅ Complete — see [research.md](./research.md)*

**Key decisions resolved**:

| Question | Decision | Rationale |
|----------|----------|-----------|
| New package name? | `Juice.Messaging.Contracts` | Matches existing `Juice.Messaging.*` namespace family; empty placeholder already exists |
| New namespace? | `Juice.Messaging` | Consistent with existing messaging layer; integration events are messaging primitives |
| Target framework? | `$(AppTargetFramework)` (net6/8/9) | Matches existing `Juice.EventBus.Contracts`; consistent versioning |
| Backward compat mechanism? | Subtype inheritance (old types extend new canonical) | Only mechanism providing both compile-time compatibility and runtime upcast; type forwarding and C# aliases cannot achieve cross-namespace CLR identity |
| Deprecation signal? | `[Obsolete(false)]` (warning) + package `<Description>` update | Warning-only preserves SC-001 (zero errors); human-readable via NuGet UI |
| Handler dispatch update? | `IntegrationEventDispatcher` uses `Juice.Messaging.IIntegrationEventHandler<T>` | Enables new-style handlers to be found; old handlers satisfy constraint via inheritance |
| `.csproj` changes scope? | Only `Juice.EventBus` and `Juice.EF.Tests.Shared` (2 files) | All other consumers reference EventBus.Contracts only transitively |

---

## Phase 1: Design & Contracts

*Status: ✅ Complete — see [data-model.md](./data-model.md), [contracts/public-api.md](./contracts/public-api.md), [quickstart.md](./quickstart.md)*

### New Package: `Juice.Messaging.Contracts`

**`Juice.Messaging.Contracts.csproj`**:
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFrameworks>$(AppTargetFramework)</TargetFrameworks>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <RootNamespace>Juice.Messaging</RootNamespace>
    <Description>Canonical contracts for Juice integration events (IIntegrationEvent, IntegrationEvent, IIntegrationEventHandler).</Description>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="..\Juice.Contracts\Juice.Contracts.csproj" />
  </ItemGroup>
</Project>
```

**`IIntegrationEvent.cs`** (in `Juice.Messaging` namespace):
```csharp
namespace Juice.Messaging
{
    public interface IIntegrationEvent : IEvent { }
}
```

**`IntegrationEvent.cs`** (in `Juice.Messaging` namespace):
```csharp
namespace Juice.Messaging
{
    public abstract record IntegrationEvent : MessageBase, IIntegrationEvent
    {
        public virtual string EventName => GetType().Name;
    }
}
```

**`IIntegrationEventHandler.cs`** (in `Juice.Messaging` namespace):
```csharp
namespace Juice.Messaging
{
    public interface IIntegrationEventHandler<in TIntegrationEvent>
        where TIntegrationEvent : IIntegrationEvent
    {
        Task HandleAsync(TIntegrationEvent @event);
    }
}
```

### Updated Shim: `Juice.EventBus.Contracts`

**`Juice.EventBus.Contracts.csproj`** — add reference + update description:
```xml
<Description>[Deprecated] Use Juice.Messaging.Contracts. This package re-exports integration event contracts for backward compatibility.</Description>
...
<ItemGroup>
  <ProjectReference Include="..\Juice.Contracts\Juice.Contracts.csproj" />
  <ProjectReference Include="..\Juice.Messaging.Contracts\Juice.Messaging.Contracts.csproj" />  <!-- NEW -->
</ItemGroup>
```

**`IIntegrationEvent.cs`** — extend canonical + deprecate:
```csharp
namespace Juice.EventBus
{
    [Obsolete("Use Juice.Messaging.IIntegrationEvent from Juice.Messaging.Contracts instead.", false)]
    public interface IIntegrationEvent : Juice.Messaging.IIntegrationEvent { }
}
```

**`IntegrationEvent.cs`** — extend canonical + deprecate:
```csharp
namespace Juice.EventBus
{
    [Obsolete("Use Juice.Messaging.IntegrationEvent from Juice.Messaging.Contracts instead.", false)]
    public abstract record IntegrationEvent : Juice.Messaging.IntegrationEvent { }
}
```

**`IIntegrationEventHandler.cs`** — add deprecation only:
```csharp
namespace Juice.EventBus
{
    [Obsolete("Use Juice.Messaging.IIntegrationEventHandler<T> from Juice.Messaging.Contracts instead.", false)]
    public interface IIntegrationEventHandler<in TIntegrationEvent>
        where TIntegrationEvent : IIntegrationEvent
    {
        Task HandleAsync(TIntegrationEvent @event);
    }
}
```

### Updated: `Juice.EventBus` (internal consumer)

**`Juice.EventBus.csproj`** — swap reference:
- Remove: `<ProjectReference ... Juice.EventBus.Contracts ... />`
- Add: `<ProjectReference ... Juice.Messaging.Contracts ... />`

**`Dispatching/IntegrationEventDispatcher.cs`** — update handler resolution:
```csharp
// Before (line ~39)
var concreteType = typeof(IIntegrationEventHandler<>).MakeGenericType(evt.GetType());
// ↑ This was Juice.EventBus.IIntegrationEventHandler<>

// After
var concreteType = typeof(Juice.Messaging.IIntegrationEventHandler<>).MakeGenericType(evt.GetType());
```

Also update `using Juice.EventBus` → `using Juice.Messaging` for contract type references
in all files within `Juice.EventBus` project.

### Updated: `Juice.EF.Tests.Shared` (test canonical usage)

**`Juice.EF.Tests.Shared.csproj`** — swap reference:
- Remove `Juice.EventBus.Contracts` reference
- Add `Juice.Messaging.Contracts` reference

**`Events/ContentPublishedIntegrationEvent.cs`** and `ContentNameChangedIntegrationEvent.cs`:
```csharp
// Before
using Juice.EventBus;

// After
using Juice.Messaging;

// Base class 'IntegrationEvent' remains the same identifier — no rename needed
[Domain("Contents")]
public record ContentPublishedIntegrationEvent : IntegrationEvent { ... }
```

### Agent Context Update

Run: `.specify/scripts/powershell/update-agent-context.ps1 -AgentType claude`

Technologies confirmed (no new additions to agent context required — this migration uses
only existing Juice framework patterns):
- C# multi-target NuGet library (`$(AppTargetFramework)`)
- `[Obsolete]` deprecation attribute
- Project reference DAG manipulation
- xUnit integration tests (no new test framework)

---

## Post-Design Constitution Check

| Principle | Status | Verification |
|-----------|--------|--------------|
| I. Lightweight & Dual-Architecture | ✅ | No new hosting or runtime coupling added |
| II. Library-First Composability | ✅ | `Juice.Messaging.Contracts` has distinct boundary; layering preserved: `Juice.Contracts → Juice.Messaging.Contracts → (optional) Juice.EventBus.Contracts`; no circular deps |
| III. DDD + CQRS | ✅ | Not applicable; no domain model changes |
| IV. Reliable Messaging | ✅ | `DeliveryWorker`, `OutboxEventService`, `IMessagePublishingPolicy` all unaffected |
| V. Multi-Tenancy First | ✅ | `TenantId` field preserved on `IntegrationEvent` via `MessageBase` inheritance chain |

All gates pass. Ready for task generation (`/speckit.tasks`).
