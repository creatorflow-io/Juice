# Implementation Plan: In-Code Publishing Policies

**Branch**: `007-in-code-policies` | **Date**: 2026-03-25 | **Spec**: [spec.md](spec.md)
**Input**: Feature specification from `/specs/007-in-code-policies/spec.md`

## Summary

Extend `MessagingBuilder` to accept publishing policy rules defined in C# code, complementing the existing JSON configuration path. Rules from both sources are merged via `IOptions<T>` accumulation into the existing `PublishingPolicyOptions` and resolved by the unmodified `DefaultEventPublishingPolicy` sort. A `bool IsCodeDefined` flag on `PublishRule` serves as a tiebreaker when code and config rules share the same priority. A third overload supports fully custom `IMessagePublishingPolicy` implementations. All changes are confined to `core/src/Juice.Messaging/`; no new library project or migration is required.

## Technical Context

**Language/Version**: C# / .NET 6, 8, 9 — libraries target `netstandard2.1`
**Primary Dependencies**: `Microsoft.Extensions.DependencyInjection`, `Microsoft.Extensions.Options`, `Microsoft.Extensions.Configuration`
**Storage**: N/A — configuration-time only; no persistent state
**Testing**: xUnit in `core/test/Juice.Messaging.Tests/` — unit tests, no infrastructure required
**Target Platform**: NuGet library (`Juice.Messaging`)
**Project Type**: Library feature
**Performance Goals**: `IMessagePublishingPolicy` is singleton; rule count is O(10s) in practice — no performance concern
**Constraints**: Zero breaking changes to existing public API; existing JSON-only deployments must work without code changes
**Scale/Scope**: Single library project; 5 files changed or created; 1 new test file

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-checked after Phase 1 design.*

| Principle | Status | Notes |
|-----------|--------|-------|
| I. Lightweight & Dual-Architecture | ✅ PASS | Purely additive overloads; no speculative abstraction; works in monolith and microservice |
| II. Library-First Composability | ✅ PASS | All changes in `core/src/Juice.Messaging/`; no new library; no circular dependency |
| III. DDD + CQRS | ✅ PASS | Infrastructure/DI layer only; domain model untouched |
| IV. Reliable Messaging via Outbox | ✅ PASS | Policy routing mechanism unchanged; only the registration path changes |
| V. Multi-Tenancy First | ✅ PASS | Code-defined rules support the same tenant/tier match criteria as config rules |

**Post-Design Re-Check**: All five principles remain satisfied. No new project, no bypass of outbox, no new broker dependencies.

## Project Structure

### Documentation (this feature)

```text
specs/007-in-code-policies/
├── plan.md              ← This file
├── research.md          ← Phase 0 output
├── data-model.md        ← Phase 1 output
├── quickstart.md        ← Phase 1 output
├── contracts/
│   └── api.md           ← Phase 1 output
└── tasks.md             ← Phase 2 output (/speckit.tasks — not created here)
```

### Source Code (repository root)

```text
core/src/Juice.Messaging/
├── Policies/
│   ├── IMessagePublishingPolicy.cs          (unchanged)
│   ├── PolicyResolveContext.cs              (unchanged)
│   ├── PublishRoute.cs                      (unchanged)
│   ├── PublishingPolicyBuilder.cs           ← NEW
│   └── Internal/
│       ├── PublishingPolicyOptions.cs       ← MODIFIED (add IsCodeDefined to PublishRule)
│       ├── DefaultEventPublishingPolicy.cs  ← MODIFIED (add IsCodeDefined tiebreaker)
│       └── PublishRuleMatchExtensions.cs    (unchanged)
└── MessagingBuilder.cs                      ← MODIFIED (two new AddPublishingPolicies overloads)

core/test/Juice.Messaging.Tests/
└── PublishingPolicyBuilderTest.cs           ← NEW
```

**Structure Decision**: Single project modification (Option 1 — single project). No new `core/src/` library is created; the feature is an additive enhancement to `Juice.Messaging`, which is the correct home for all publishing policy concerns per the existing layer structure.
