# Tasks: EventBus Contracts → Messaging Contracts Migration

**Input**: Design documents from `/specs/001-eventbus-messaging-migration/`
**Branch**: `001-eventbus-messaging-migration` | **Generated**: 2026-02-24
**Prerequisites**: plan.md ✅ | spec.md ✅ | research.md ✅ | data-model.md ✅ | contracts/public-api.md ✅ | quickstart.md ✅

**Tests**: Not explicitly requested. Existing tests in `Juice.Integrations.Tests` and `Juice.EventBus.Tests` serve as the acceptance suite (SC-004). No new test tasks generated.

**Organization**: Tasks are grouped by user story. US1 creates the canonical package (foundational for US2). US2 makes the deprecated shim and migrates internal consumers. US3 verifies mixed-usage compilation.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (US1, US2, US3)
- All paths relative to repository root

---

## Phase 1: Setup

**Purpose**: Register the new project in the solution so it is included in all `dotnet build` and `dotnet test` invocations.

- [x] T001 Add `core/src/Juice.Messaging.Contracts/Juice.Messaging.Contracts.csproj` to `Juice.sln` (new project entry under the `core/src` solution folder, same pattern as adjacent projects)

**Checkpoint**: `Juice.sln` references the new project; `dotnet sln list` shows `Juice.Messaging.Contracts`. ✅

---

## Phase 2: User Story 1 — New consumers adopt Messaging Contracts (Priority: P1) 🎯 MVP

**Goal**: Create the canonical `Juice.Messaging.Contracts` NuGet library containing `IIntegrationEvent`, `IntegrationEvent`, and `IIntegrationEventHandler<T>` under the `Juice.Messaging` namespace, with a project reference to `Juice.Contracts`.

**Independent Test**: After this phase, `dotnet build core/src/Juice.Messaging.Contracts/Juice.Messaging.Contracts.csproj` succeeds with zero errors. The three public types are present in the `Juice.Messaging` namespace with the contract shape specified in `contracts/public-api.md`.

### Implementation for User Story 1

- [x] T002 [US1] Create `core/src/Juice.Messaging.Contracts/Juice.Messaging.Contracts.csproj` with `TargetFrameworks=$(AppTargetFramework)`, `RootNamespace=Juice.Messaging`, `Nullable=enable`, `ImplicitUsings=enable`, Description="Canonical contracts for Juice integration events (IIntegrationEvent, IntegrationEvent, IIntegrationEventHandler).", and a `ProjectReference` to `../Juice.Contracts/Juice.Contracts.csproj`
- [x] T003 [P] [US1] Create `core/src/Juice.Messaging.Contracts/IIntegrationEvent.cs` — `namespace Juice.Messaging` — `public interface IIntegrationEvent : IEvent { }` (IEvent from Juice.Contracts)
- [x] T004 [P] [US1] Create `core/src/Juice.Messaging.Contracts/IntegrationEvent.cs` — `namespace Juice.Messaging` — `public abstract record IntegrationEvent : MessageBase, IIntegrationEvent` with `public virtual string EventName => GetType().Name;` (MessageBase from Juice.Contracts)
- [x] T005 [P] [US1] Create `core/src/Juice.Messaging.Contracts/IIntegrationEventHandler.cs` — `namespace Juice.Messaging` — `public interface IIntegrationEventHandler<in TIntegrationEvent> where TIntegrationEvent : IIntegrationEvent` with `Task HandleAsync(TIntegrationEvent @event);`

**Checkpoint**: `dotnet build core/src/Juice.Messaging.Contracts/` succeeds. ✅

---

## Phase 3: User Story 2 — Existing consumers continue working without code changes (Priority: P1)

**Goal**: Update `Juice.EventBus.Contracts` to be a deprecated shim whose types extend the canonical `Juice.Messaging` types. Update `Juice.EventBus` (the internal consumer) to reference `Juice.Messaging.Contracts` and resolve handlers via the canonical interface. Update `Juice.EF.Tests.Shared` to demonstrate canonical usage.

**Depends on**: Phase 2 complete (US1) — the shim references `Juice.Messaging.Contracts`.

**Independent Test**: After this phase, `dotnet build Juice.sln` succeeds with zero errors. Existing code that uses `Juice.EventBus.IIntegrationEvent`, `Juice.EventBus.IntegrationEvent`, and `Juice.EventBus.IIntegrationEventHandler<T>` receives deprecation warnings (CS0618) but no errors (SC-001). Existing tests in `Juice.EventBus.Tests` and `Juice.Integrations.Tests` pass without modification (SC-004).

### Update Juice.EventBus.Contracts Shim

- [x] T006 [US2] Update `core/src/Juice.EventBus.Contracts/Juice.EventBus.Contracts.csproj`: add `<ProjectReference Include="../Juice.Messaging.Contracts/Juice.Messaging.Contracts.csproj" />` and update `<Description>` to `[Deprecated] Use Juice.Messaging.Contracts instead. This package re-exports integration event contracts from Juice.Messaging.Contracts for backward compatibility.`
- [x] T007 [P] [US2] Update `core/src/Juice.EventBus.Contracts/IIntegrationEvent.cs`: change base from `: IEvent` to `: Juice.Messaging.IIntegrationEvent`; add `[Obsolete("Use Juice.Messaging.IIntegrationEvent from Juice.Messaging.Contracts instead.", false)]` on the interface; remove the `IEvent` using if it is no longer needed directly
- [x] T008 [P] [US2] Update `core/src/Juice.EventBus.Contracts/IntegrationEvent.cs`: change base from `: MessageBase, IIntegrationEvent` to `: Juice.Messaging.IntegrationEvent`; remove the now-inherited `EventName` property (it is already defined on `Juice.Messaging.IntegrationEvent`); add `[Obsolete("Use Juice.Messaging.IntegrationEvent from Juice.Messaging.Contracts instead.", false)]` on the record
- [x] T009 [P] [US2] Update `core/src/Juice.EventBus.Contracts/IIntegrationEventHandler.cs`: add `[Obsolete("Use Juice.Messaging.IIntegrationEventHandler<T> from Juice.Messaging.Contracts instead.", false)]` on the interface; keep the `where T : IIntegrationEvent` constraint unchanged (it refers to `Juice.EventBus.IIntegrationEvent` which now extends the canonical one)

### Update Juice.EventBus Internal Consumer

- [x] T010 [US2] Update `core/src/Juice.EventBus/Juice.EventBus.csproj`: replace `<ProjectReference ... Juice.EventBus.Contracts .../>` with `<ProjectReference Include="../Juice.Messaging.Contracts/Juice.Messaging.Contracts.csproj" />`
- [x] T011 [US2] Update `core/src/Juice.EventBus/Dispatching/IntegrationEventDispatcher.cs`: change handler resolution from `typeof(IIntegrationEventHandler<>)` (Juice.EventBus namespace) to `typeof(Juice.Messaging.IIntegrationEventHandler<>)`; update or add `using Juice.Messaging;` and remove `using Juice.EventBus;` where it referred to contract types
- [x] T012 [US2] Update all remaining `.cs` files in `core/src/Juice.EventBus/` that have `using Juice.EventBus;` solely for contract types (`IIntegrationEvent`, `IntegrationEvent`, `IIntegrationEventHandler<T>`): replace those `using` directives with `using Juice.Messaging;` (approximately 5 files: `IEventBus.cs`, event bus implementation, DI extension files; dispatcher was handled in T011)

### Update Juice.EF.Tests.Shared

- [x] T013 [US2] Update `core/test/Juice.EF.Tests.Shared/Juice.EF.Tests.Shared.csproj`: replace `<ProjectReference ... Juice.EventBus.Contracts .../>` with `<ProjectReference Include="../../src/Juice.Messaging.Contracts/Juice.Messaging.Contracts.csproj" />`
- [x] T014 [P] [US2] Update `core/test/Juice.EF.Tests.Shared/Events/ContentPublishedIntegrationEvent.cs`: replace `using Juice.EventBus;` with `using Juice.Messaging;`; base class identifier `IntegrationEvent` remains unchanged (resolves to `Juice.Messaging.IntegrationEvent` after the using change)
- [x] T015 [P] [US2] Update `core/test/Juice.EF.Tests.Shared/Events/ContentNameChangedIntegrationEvent.cs`: same change as T014 — replace `using Juice.EventBus;` with `using Juice.Messaging;`

**Checkpoint**: `dotnet build Juice.sln` → zero errors. ✅

---

## Phase 4: User Story 3 — Gradual migration path for existing consumers (Priority: P2)

**Goal**: Confirm that a project referencing both `Juice.EventBus.Contracts` and `Juice.Messaging.Contracts` simultaneously compiles without type-resolution conflicts or ambiguity errors, and that old-style events (inheriting `Juice.EventBus.IntegrationEvent`) satisfy the `Juice.Messaging.IIntegrationEvent` constraint at runtime.

**Depends on**: Phase 3 complete (US2) — the shim subtype hierarchy must be in place.

**Independent Test**: SC-003 — build Juice.sln (which includes projects referencing both packages transitively) produces zero errors. Existing `Juice.EventBus.Tests` and `Juice.Integrations.Tests` pass, demonstrating that old-style handler implementations are dispatched correctly via `Juice.Messaging.IIntegrationEventHandler<T>` resolution.

### Implementation for User Story 3

- [x] T016 [US3] Run `dotnet build Juice.sln` and verify zero errors; confirm CS0618 warnings appear only where expected (files still using `Juice.EventBus` contract types) and no CS0121 (ambiguous type) errors appear in any project
- [x] T017 [US3] Run `dotnet test core/test/Juice.EventBus.Tests/` and `dotnet test core/test/Juice.Integrations.Tests/` (skipping infrastructure-dependent tests where needed); confirm all tests that previously passed still pass — this validates runtime dispatch compatibility (old handlers found via `Juice.Messaging.IIntegrationEventHandler<T>` resolution, old events upcast to canonical interface)

**Checkpoint**: SC-003 and SC-004 confirmed. ✅

---

## Phase 5: Polish & Cross-Cutting Concerns

**Purpose**: Final verification, quickstart validation, and solution-wide hygiene.

- [x] T018 [P] Run `dotnet build Juice.sln` one final time from repo root and confirm zero errors and no unexpected warnings beyond CS0618 on known deprecated usages
- [x] T019 [P] Validate `specs/001-eventbus-messaging-migration/quickstart.md` checklist: confirm a project referencing only `Juice.Messaging.Contracts` can define `StockDepletedEvent : IntegrationEvent` and `StockDepletedHandler : IIntegrationEventHandler<StockDepletedEvent>` as shown in the guide, and that `dotnet build` succeeds
- [x] T020 Verify `Juice.Messaging.Contracts` appears correctly in `dotnet sln list` output and that no stale references to the empty placeholder directory remain

---

## Dependencies & Execution Order

### Phase Dependencies

- **Phase 1 (Setup)**: No dependencies — start immediately
- **Phase 2 (US1)**: Depends on Phase 1 (project must be in sln before building)
- **Phase 3 (US2)**: Depends on Phase 2 complete — shim `csproj` references canonical project
- **Phase 4 (US3)**: Depends on Phase 3 complete — verifies the combined result
- **Phase 5 (Polish)**: Depends on Phase 4 complete

### User Story Dependencies

- **US1 (P1)**: Unblocked after Setup — creates the canonical package
- **US2 (P1)**: Blocked by US1 — shim types extend canonical types; internal consumers swap reference
- **US3 (P2)**: Blocked by US2 — validates the combined US1 + US2 structural outcome

### Within Each Phase

- T002 must complete before T003, T004, T005 (project file before source files)
- T006 must complete before T007, T008, T009 (project file update before source changes)
- T010 must complete before T011, T012 (project file before source)
- T013 must complete before T014, T015 (project file before source)
- T016 must complete before T017 (build before test)

### Parallel Opportunities

**Phase 2 (US1)**: After T002 completes, T003 + T004 + T005 can all run in parallel (separate files).

**Phase 3 (US2)**:
- After T006: T007 + T008 + T009 can run in parallel (separate files)
- After T010: T011 and T012 are sequential (T011 first, then T012 sweeps remaining files)
- After T013: T014 + T015 can run in parallel (separate files)

---

## Parallel Example: User Story 1

```bash
# After T002 (csproj created), launch all three source files in parallel:
Task: "Create IIntegrationEvent.cs in core/src/Juice.Messaging.Contracts/"       # T003
Task: "Create IntegrationEvent.cs in core/src/Juice.Messaging.Contracts/"        # T004
Task: "Create IIntegrationEventHandler.cs in core/src/Juice.Messaging.Contracts/" # T005
```

## Parallel Example: User Story 2 — Shim update

```bash
# After T006 (csproj updated), launch all three shim source files in parallel:
Task: "Update IIntegrationEvent.cs in core/src/Juice.EventBus.Contracts/"  # T007
Task: "Update IntegrationEvent.cs in core/src/Juice.EventBus.Contracts/"   # T008
Task: "Update IIntegrationEventHandler.cs in core/src/Juice.EventBus.Contracts/" # T009
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup (T001)
2. Complete Phase 2: User Story 1 (T002–T005)
3. **STOP and VALIDATE**: `dotnet build core/src/Juice.Messaging.Contracts/` succeeds; canonical types are usable in a standalone project

### Incremental Delivery

1. Phase 1 + Phase 2 → Canonical package exists (US1 complete)
2. Phase 3 → Shim + internal consumers updated (US2 complete) → `dotnet build Juice.sln` passes
3. Phase 4 → Mixed usage verified (US3 complete) → tests pass
4. Phase 5 → Final validation

### Single-Developer Sequential Strategy

```
T001 → T002 → T003+T004+T005 (parallel) → T006 → T007+T008+T009 (parallel)
     → T010 → T011 → T012 → T013 → T014+T015 (parallel)
     → T016 → T017 → T018+T019 (parallel) → T020
```

---

## Notes

- **No new test projects** — existing `Juice.EventBus.Tests` and `Juice.Integrations.Tests` are the acceptance suite per SC-004 and plan.md decision
- **CS0618 warnings are expected** on any project still using `Juice.EventBus` contract namespaces (e.g., `Juice.EventBus.RabbitMQ`, test projects not in scope for using-statement migration) — these are deprecation warnings, not errors
- **`Juice.EventBus.RabbitMQ` does NOT need csproj changes** — it references `Juice.EventBus` (which now references `Juice.Messaging.Contracts`), so it gets the canonical types transitively
- **IntegrationEvent.cs shim simplification** (T008): since `Juice.Messaging.IntegrationEvent` already has `EventName => GetType().Name`, the shim record body may be entirely empty — verify and remove the duplicate property override if present in the current file
- [P] tasks = different files, no shared state dependencies
- Commit after each phase checkpoint for easy rollback if needed
- **Implementation note**: Additional files beyond the original task scope required `using Juice.Messaging;` due to implicit parent namespace resolution — fixed in `Juice.EventBus.RabbitMQ/Consuming/RabbitMQConsumerBuilder.cs`, `test/Juice.Tests.Host/` (5 files), and `core/test/Juice.EventBus.Tests/` (8 files)
