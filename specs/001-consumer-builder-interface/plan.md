# Implementation Plan: Consumer Builder Interface

**Branch**: `001-consumer-builder-interface` | **Date**: 2026-04-09 | **Spec**: [spec.md](spec.md)
**Input**: Feature specification from `/specs/001-consumer-builder-interface/spec.md`

## Summary

Introduce a public `IConsumerBuilder` interface in `Juice.EventBus` that both `RabbitMQConsumerBuilder` and `LocalConsumerBuilder` implement, exposing `Subscribe<TEvent, THandler>(string? route = default)` for fluent chaining. Both concrete builders keep their existing public API intact via explicit interface implementations; no new project references are required.

## Technical Context

**Language/Version**: C# on .NET 6, 8, 9, 10 — targets `netstandard2.1` for library projects
**Primary Dependencies**: `Juice.EventBus` (shared abstractions); `Juice.EventBus.RabbitMQ` and `Juice.Messaging.Local` (concrete builders)
**Storage**: N/A — no persistence, no migrations
**Testing**: xUnit; `IgnoreOnCIFact` for infra-dependent tests; `[InitializeMessageContext]` not required (no outbox/messaging flow)
**Target Platform**: .NET library (NuGet package)
**Project Type**: Library — public API surface change
**Performance Goals**: N/A — interface introduction has no runtime overhead
**Constraints**: No breaking changes to existing public API; no new project references added
**Scale/Scope**: 3 files changed (1 new, 2 modified); 1 new interface, 2 explicit interface implementations

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Status | Notes |
|-----------|--------|-------|
| I. Lightweight & Dual-Architecture | ✅ PASS | Interface serves a concrete use case (shared extension methods); no speculative complexity; builders usable in both microservice and monolith compositions |
| II. Library-First Composability | ✅ PASS | `IConsumerBuilder` placed in existing `Juice.EventBus` — no new project created; no circular dependencies introduced; interface exposes functionality through typed contract |
| III. DDD + CQRS | ✅ PASS | No domain model changes; no mediator involvement |
| IV. Reliable Messaging via Outbox | ✅ PASS | No changes to outbox, delivery, or publishing flows |
| V. Multi-Tenancy First | ✅ PASS | No tenant-resolution changes |

**Post-design re-check**: All gates still pass. No complexity violations.

## Project Structure

### Documentation (this feature)

```text
specs/001-consumer-builder-interface/
├── plan.md              ← this file
├── research.md          ← Phase 0: assembly placement, return-type pattern, param naming
├── data-model.md        ← Phase 1: type model, modified builders, dependency graph
├── quickstart.md        ← Phase 1: before/after usage examples
├── contracts/
│   └── IConsumerBuilder.cs   ← intended public contract (reference, not compiled)
└── tasks.md             ← Phase 2 output (/speckit.tasks command)
```

### Source Code (repository root)

```text
core/src/Juice.EventBus/
└── IConsumerBuilder.cs          ← NEW: public interface

core/src/Juice.EventBus.RabbitMQ/Consuming/
└── RabbitMQConsumerBuilder.cs   ← MODIFY: add IConsumerBuilder, explicit impl

core/src/Juice.Messaging.Local/Internal/
└── LocalConsumerBuilder.cs      ← MODIFY: add IConsumerBuilder, explicit impl

core/test/Juice.EventBus.Tests/  ← MODIFY: add interface-level tests (if project exists)
                                    or extend nearest integration test project
```

**Structure Decision**: Single-library pattern (`core/src/` + `core/test/`). Three file changes total; no new projects.

## Implementation Notes

### Interface definition

```csharp
// core/src/Juice.EventBus/IConsumerBuilder.cs
namespace Juice.EventBus;

public interface IConsumerBuilder
{
    IConsumerBuilder Subscribe<TEvent, THandler>(string? route = default)
        where TEvent : IIntegrationEvent
        where THandler : class, IIntegrationEventHandler<TEvent>;
}
```

### Explicit interface implementations (pattern for both builders)

```csharp
// Added to RabbitMQConsumerBuilder (already has the concrete Subscribe returning its own type)
IConsumerBuilder IConsumerBuilder.Subscribe<TEvent, THandler>(string? route)
    => Subscribe<TEvent, THandler>(route);

// Added to LocalConsumerBuilder (uses 'key' internally — route maps to key)
IConsumerBuilder IConsumerBuilder.Subscribe<TEvent, THandler>(string? route)
    => Subscribe<TEvent, THandler>(route);
```

The concrete methods (`Subscribe<>()` returning the concrete type) remain untouched.

### No breaking changes

- Existing call sites holding concrete types continue to compile.
- `RabbitMQConsumerBuilder.ConfigureQos()` and other RabbitMQ-specific methods remain accessible on the concrete type.
- `LocalConsumerBuilder`'s `ILocalSubscriptionsProvider` implementation is unaffected.

## Complexity Tracking

No constitution violations — complexity table not required.
