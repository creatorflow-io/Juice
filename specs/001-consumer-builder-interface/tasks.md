# Tasks: Consumer Builder Interface

**Input**: Design documents from `/specs/001-consumer-builder-interface/`
**Prerequisites**: plan.md ✅, spec.md ✅, research.md ✅, data-model.md ✅, contracts/ ✅

**Organization**: Tasks are grouped by user story to enable independent implementation and testing of each story.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (US1, US2)

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Confirm working branch and locate all files to be modified/created

- [x] T001 Confirm branch `001-consumer-builder-interface` is active and working tree is clean
- [x] T002 [P] Locate and read `core/src/Juice.EventBus/Juice.EventBus.csproj` — verify no reference to `Juice.EventBus.RabbitMQ` or `Juice.Messaging.Local` (would indicate circular-dep risk)
- [x] T003 [P] Read `core/src/Juice.EventBus.RabbitMQ/Consuming/RabbitMQConsumerBuilder.cs` and `core/src/Juice.Messaging.Local/Internal/LocalConsumerBuilder.cs` — capture exact current signatures before editing

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Create `IConsumerBuilder` — the single prerequisite that blocks both user story phases

**⚠️ CRITICAL**: No user story work can begin until this phase is complete

- [x] T004 Create `core/src/Juice.EventBus/IConsumerBuilder.cs` with `IConsumerBuilder` interface declaring `Subscribe<TEvent, THandler>(string? route = default) : IConsumerBuilder` with constraints `where TEvent : IIntegrationEvent where THandler : class, IIntegrationEventHandler<TEvent>` and full XML doc comment

**Checkpoint**: `IConsumerBuilder` exists in `Juice.EventBus` — user story implementation can now begin

---

## Phase 3: User Story 1 — Both Builders Implement IConsumerBuilder (Priority: P1) 🎯 MVP

**Goal**: `RabbitMQConsumerBuilder` and `LocalConsumerBuilder` both implement `IConsumerBuilder` so either can be assigned to an `IConsumerBuilder` variable without cast, and fluent chaining is preserved.

**Independent Test**: Assign each concrete builder to an `IConsumerBuilder` variable and call `.Subscribe<>()` on it — the returned value must be `IConsumerBuilder` and all subscriptions must be registered.

### Implementation for User Story 1

- [x] T005 [P] [US1] In `core/src/Juice.EventBus.RabbitMQ/Consuming/RabbitMQConsumerBuilder.cs`: add `: IConsumerBuilder` to the class declaration and add the explicit interface implementation `IConsumerBuilder IConsumerBuilder.Subscribe<TEvent, THandler>(string? route) => Subscribe<TEvent, THandler>(route);`
- [x] T006 [P] [US1] In `core/src/Juice.Messaging.Local/Internal/LocalConsumerBuilder.cs`: add `: IConsumerBuilder` to the class declaration and add the explicit interface implementation `IConsumerBuilder IConsumerBuilder.Subscribe<TEvent, THandler>(string? route) => Subscribe<TEvent, THandler>(route);`
- [x] T007 [US1] Build the solution (`dotnet build`) targeting all TFMs and confirm zero new errors or warnings in `Juice.EventBus`, `Juice.EventBus.RabbitMQ`, and `Juice.Messaging.Local`

**Checkpoint**: Both builders compile as `IConsumerBuilder` implementors; existing tests still pass

---

## Phase 4: User Story 2 — Shared Extension Methods via IConsumerBuilder (Priority: P2)

**Goal**: A single extension method written against `IConsumerBuilder` works with both concrete builders, eliminating duplicated registration code.

**Independent Test**: Define `static IConsumerBuilder SubscribeAll(this IConsumerBuilder b)` that chains multiple `Subscribe<>()` calls; invoke it on both a `RabbitMQConsumerBuilder` and a `LocalConsumerBuilder` instance in a test and verify subscriptions are registered on each.

### Implementation for User Story 2

- [x] T008 [US2] Locate the nearest shared test project (e.g., `core/test/Juice.EventBus.Tests/` or `core/test/Juice.Messaging.Tests/`) and add a compile-time + runtime test class `ConsumerBuilderInterfaceTests` that:
  1. Creates a `RabbitMQConsumerBuilder` instance and assigns it to `IConsumerBuilder`
  2. Creates a `LocalConsumerBuilder` instance and assigns it to `IConsumerBuilder`
  3. Defines a local helper `static IConsumerBuilder SubscribeAll(IConsumerBuilder b) => b.Subscribe<SomeEvent, SomeHandler>()` and invokes it on both
  4. Verifies fluent chaining returns `IConsumerBuilder` (the test itself is the compilation evidence)
- [x] T009 [US2] Run the test project containing `ConsumerBuilderInterfaceTests` and confirm all assertions pass

**Checkpoint**: Extension method pattern compiles and runs correctly against both builders

---

## Phase 5: Polish & Cross-Cutting Concerns

**Purpose**: Build hygiene, docs, and verification across all target frameworks

- [x] T010 [P] Run full solution build for all target frameworks (`dotnet build -c Release`) and confirm zero errors
- [x] T011 [P] Run existing test suite for `Juice.EventBus.RabbitMQ` and `Juice.Messaging.Local` to confirm no regressions (use `IgnoreOnCIFact` guard; broker availability is not required)
- [x] T012 Verify `IConsumerBuilder` XML doc comment renders correctly in IDE tooltips (review generated XML output in `bin/`)
- [x] T013 Update `CLAUDE.md` active feature context if agent context script did not capture `IConsumerBuilder`

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — can start immediately
- **Foundational (Phase 2)**: Depends on Phase 1 completion — **BLOCKS both user stories**
- **US1 (Phase 3)**: Depends on Phase 2 (interface must exist before builders implement it); T005 and T006 are parallel
- **US2 (Phase 4)**: Depends on Phase 3 (both builders must implement the interface before the extension method test is valid)
- **Polish (Phase 5)**: Depends on Phase 4 completion

### User Story Dependencies

- **User Story 1 (P1)**: Requires `IConsumerBuilder` interface (Phase 2) — no other story dependency
- **User Story 2 (P2)**: Requires US1 complete — both builders must implement the interface before testing the shared extension method pattern

### Within Each User Story

- **US1**: T005 and T006 are fully parallel (different files); T007 depends on both
- **US2**: T008 and T009 are sequential (test must exist before it can be run)

### Parallel Opportunities

- T002 and T003 (Phase 1 reads) run in parallel
- T005 and T006 (Phase 3 builder modifications) run in parallel — different files
- T010 and T011 (Phase 5 build + test runs) run in parallel

---

## Parallel Example: User Story 1

```bash
# Both builder modifications are independent — launch together:
Task T005: "Add IConsumerBuilder to RabbitMQConsumerBuilder in core/src/Juice.EventBus.RabbitMQ/Consuming/RabbitMQConsumerBuilder.cs"
Task T006: "Add IConsumerBuilder to LocalConsumerBuilder in core/src/Juice.Messaging.Local/Internal/LocalConsumerBuilder.cs"

# Then, after both complete:
Task T007: "dotnet build — verify zero new errors"
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup (read files, confirm branch)
2. Complete Phase 2: Create `IConsumerBuilder` in `Juice.EventBus`
3. Complete Phase 3: Make both builders implement the interface
4. **STOP and VALIDATE**: `dotnet build` passes; assign concrete builders to `IConsumerBuilder` in a scratch test
5. Ship as MVP — the core contract is fulfilled

### Incremental Delivery

1. Phases 1–3 → Both builders implement `IConsumerBuilder` (MVP)
2. Phase 4 → Extension method pattern verified by test
3. Phase 5 → Full polish, regression confirmation, release-ready

---

## Notes

- No new `.csproj` files required — zero project additions
- Existing callers of the concrete `Subscribe<>()` methods are unaffected (explicit interface impl is invisible on the concrete type)
- `LocalConsumerBuilder` uses `key` internally; the explicit impl maps `route` → `key` transparently
- All infra-dependent tests must use `IgnoreOnCIFact` — the US2 test should be pure in-memory and needs no guard
