# Implementation Plan: Per-Processor Delivery Policy

**Branch**: `009-processor-delivery-policy` | **Date**: 2026-03-27 | **Spec**: [spec.md](spec.md)
**Input**: Feature specification from `/specs/009-processor-delivery-policy/spec.md`

## Summary

Allow `DeliveryProcessorBuilder` to accept inline `AddDeliveryPolicies` calls that scope delivery policy configuration to a specific processor (publisher + context type). At resolution time the processor-scoped policy is applied first, merging on top of the globally resolved policy, so unset fields fall through to global/wildcard matches and finally to built-in defaults.

All changes are confined to `Juice.Messaging.Outbox.Delivery` — no new projects, no DB changes, no migrations.

## Technical Context

**Language/Version**: C# on .NET 8 / .NET 9 (library targets `netstandard2.1`; runnable hosts target `net8.0;net9.0`)
**Primary Dependencies**: `Microsoft.Extensions.DependencyInjection`, `Microsoft.Extensions.Options` (named options), `Microsoft.Extensions.Hosting`
**Storage**: N/A — no DB changes
**Testing**: xUnit + FluentAssertions; existing `DeliveryPoliciesTest.cs` in `core/test/Juice.EventBus.Tests/`
**Target Platform**: .NET runtime (library — consumed by any host)
**Project Type**: Library (NuGet package)
**Performance Goals**: Policy resolution is a one-time startup cost per worker; no throughput impact
**Constraints**: Zero breaking changes to existing public API; no new public types required
**Scale/Scope**: Single project change; ~5 files touched

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Status | Notes |
|-----------|--------|-------|
| I. Lightweight & Dual-Architecture | ✅ PASS | No new abstractions; processor-scoped options are an opt-in layered on top of global config. Zero overhead when not used. |
| II. Library-First Composability | ✅ PASS | All changes inside `core/src/Juice.Messaging.Outbox.Delivery/`. No circular dependencies introduced. No new library needed. |
| III. DDD + CQRS | ✅ PASS | No domain model changes. Delivery policy is infrastructure-layer configuration. |
| IV. Reliable Messaging via Outbox | ✅ PASS | Policy config key pattern `"PublisherKey:IntentName:ContextTypeName"` is preserved. Processor-scoped layer sits above the existing resolution chain; outbox delivery mechanics unchanged. |
| V. Multi-Tenancy First | ✅ PASS | No tenant-specific changes needed; delivery processors are per-process (not per-tenant). |

**Post-design re-check**: All five principles remain satisfied. No deviations.

## Project Structure

### Documentation (this feature)

```text
specs/009-processor-delivery-policy/
├── plan.md              # This file
├── research.md          # Phase 0 — design decisions
├── data-model.md        # Phase 1 — conceptual model
├── quickstart.md        # Phase 1 — usage examples
├── contracts/
│   └── delivery-processor-builder-api.md
└── tasks.md             # Phase 2 output (/speckit.tasks command)
```

### Source Code

```text
core/src/Juice.Messaging.Outbox.Delivery/
├── DeliveryBuilder.cs                         [MODIFY] call RegisterProcessorPolicies<TContext>()
├── Processing/
│   └── DeliveryProcessorBuilder.cs            [MODIFY] add AddDeliveryPolicies + RegisterProcessorPolicies
├── Internal/
│   ├── DeliveryPolicyConfiguration.cs         [MODIFY] check processor-scoped named options first
│   ├── ProcessorPolicyRegistry.cs             [NEW]    tracks which processor keys have explicit policies
│   └── DeliveryPolicyOptions.cs               [UNMODIFIED]

core/test/Juice.EventBus.Tests/
└── DeliveryPoliciesTest.cs                    [MODIFY] add per-processor policy tests
```

**Structure Decision**: Single existing project. All modifications are additive (new methods + one new internal class). Existing public API is unchanged.

## Complexity Tracking

No constitution violations — table omitted.
