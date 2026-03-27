# Tasks: Per-Processor Delivery Policy

**Input**: Design documents from `/specs/009-processor-delivery-policy/`
**Prerequisites**: plan.md ✅, spec.md ✅, research.md ✅, data-model.md ✅, contracts/ ✅

**Tests**: Included — the existing codebase has `DeliveryPoliciesTest.cs` and the research.md explicitly lists 3 new tests required.

**Organization**: Grouped by user story; each story is independently testable.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no blocking dependencies)
- **[Story]**: User story label (US1, US2, US3)

---

## Phase 1: Setup

> No new projects or infrastructure setup required — all changes are additive modifications to the existing `Juice.Messaging.Outbox.Delivery` project.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: `ProcessorPolicyRegistry` is a shared dependency used by both `DeliveryProcessorBuilder` (write path) and `DeliveryPolicyConfiguration` (read path). It MUST exist before any user story work begins.

**⚠️ CRITICAL**: Phases 3–5 cannot begin until this phase is complete.

- [x] T001 Create internal class `ProcessorPolicyRegistry` (`HashSet<string>` wrapper with `Register(key)` and `IsConfigured(key)` methods, plus `GetOrCreateRegistry(IServiceCollection)` static helper using the instance-singleton pattern) in `core/src/Juice.Messaging.Outbox.Delivery/Internal/ProcessorPolicyRegistry.cs`

**Checkpoint**: `ProcessorPolicyRegistry` compiled and available — user story implementation can begin.

---

## Phase 3: User Story 1 — Per-Processor Policy Configuration (Priority: P1) 🎯 MVP

**Goal**: Developers can call `proc.AddDeliveryPolicies(...)` inline when registering a processor. The configured policy takes effect for that processor's delivery loop. Global config-section key-matched entries still outrank processor code policies.

**Independent Test**: Register a processor with `BatchSize = 50` and no global config; verify `IDeliveryPolicyResolver.GetPolicyAsync` returns `BatchSize = 50`. Also register a matching global config-section key entry with `BatchSize = 30` and verify `30` wins.

### Implementation for User Story 1

- [x] T002 [US1] Add `_sectionConfigures` (`List<IConfigurationSection>`) and `_policyConfigures` (`List<Action<DeliveryPolicyOptions>>`) storage fields to `DeliveryProcessorBuilder` in `core/src/Juice.Messaging.Outbox.Delivery/Processing/DeliveryProcessorBuilder.cs`

- [x] T003 [US1] Add `AddDeliveryPolicies(IConfigurationSection policies)` method to `DeliveryProcessorBuilder` — appends to `_sectionConfigures` and returns `this`; add `AddDeliveryPolicies(Action<DeliveryPolicyOptions> configure)` method — appends to `_policyConfigures` and returns `this`; in `core/src/Juice.Messaging.Outbox.Delivery/Processing/DeliveryProcessorBuilder.cs`

- [x] T004 [US1] Add `internal void RegisterProcessorPolicies<TContext>(IServiceCollection services)` method to `DeliveryProcessorBuilder` in `core/src/Juice.Messaging.Outbox.Delivery/Processing/DeliveryProcessorBuilder.cs`: if no configures stored → return (no-op); else compute `processorKey = $"{_publisher}:{typeof(TContext).Name}"`, get-or-create `ProcessorPolicyRegistry` via static helper and call `registry.Register(processorKey)`, then call `services.Configure<DeliveryPolicyOptions>(processorKey, section)` for each `_sectionConfigures` entry and `services.Configure<DeliveryPolicyOptions>(processorKey, action)` for each `_policyConfigures` entry

- [x] T005 [P] [US1] Update `DeliveryBuilder.AddDeliveryProcessor<TContext>(string publisher, Action<DeliveryProcessorBuilder>? configure)` in `core/src/Juice.Messaging.Outbox.Delivery/DeliveryBuilder.cs`: after `configure?.Invoke(builder)` and before `_services.AddHostedService(...)`, call `builder.RegisterProcessorPolicies<TContext>(_services)`

- [x] T006 [P] [US1] Update `DeliveryBuilder.AddDeliveryProcessor<TContext>(string publisher, params string[] intents)` in `core/src/Juice.Messaging.Outbox.Delivery/DeliveryBuilder.cs`: after `builder.WithIntents(intents)` and before `_services.AddHostedService(...)`, call `builder.RegisterProcessorPolicies<TContext>(_services)`

- [x] T007 [US1] Update `DeliveryPolicyConfiguration` constructor in `core/src/Juice.Messaging.Outbox.Delivery/Internal/DeliveryPolicyConfiguration.cs` to accept two new optional parameters: `ProcessorPolicyRegistry? processorRegistry = null` and `IOptionsMonitor<DeliveryPolicyOptions>? optionsMonitor = null`; store both as private fields

- [x] T008 [US1] In `DeliveryPolicyConfiguration.GetPolicyAsync` in `core/src/Juice.Messaging.Outbox.Delivery/Internal/DeliveryPolicyConfiguration.cs`: after the four existing key-match lookups (`exactKey`, `publisherIntentKey`, `publisherKey`, `intentKey`) and before the `DefaultPolicy` check, insert a processor code policy check: if `_processorRegistry?.IsConfigured($"{context.PublisherKey}:{context.Context}") == true` then get `processorOpts = _optionsMonitor!.Get(processorKey)` and if `processorOpts.DefaultPolicy != null` return `processorOpts.DefaultPolicy.ToPolicy(_options.DefaultPolicy?.ToPolicy())`

- [x] T009 [US1] Add unit test `Processor_Code_Policy_Used_When_No_Global_Key_MatchAsync` in `core/test/Juice.EventBus.Tests/DeliveryPoliciesTest.cs`: register a processor with `BatchSize = 50` via delegate, no global Policies dictionary entries; resolve and assert `policy.BatchSize == 50`

- [x] T010 [US1] Add unit test `Global_Config_Section_Key_Match_Overrides_Processor_Code_PolicyAsync` in `core/test/Juice.EventBus.Tests/DeliveryPoliciesTest.cs`: register processor code policy `BatchSize = 50` AND global config section with key `"rabbitmq:send-pending:TestContext": { BatchSize: 30 }`; resolve and assert `policy.BatchSize == 30`

**Checkpoint**: US1 fully functional — processor inline policies work and global config-section key entries still win.

---

## Phase 4: User Story 2 — Global Fallback When No Processor Policy Exists (Priority: P2)

**Goal**: Processors registered without any `AddDeliveryPolicies` call automatically use the globally configured policies. Zero behavioral change for existing code.

**Independent Test**: Register a processor with no `AddDeliveryPolicies` call; configure global policy with `BatchSize = 20`; verify resolved policy uses `BatchSize = 20`.

### Implementation for User Story 2

> No new implementation code required — the fallback is automatic once Phase 3 is complete (`ProcessorPolicyRegistry.IsConfigured` returns false → existing global lookup runs unchanged).

- [x] T011 [US2] Add unit test `Processor_Without_Code_Policy_Falls_Back_To_GlobalAsync` in `core/test/Juice.EventBus.Tests/DeliveryPoliciesTest.cs`: register a processor via `AddDeliveryProcessor<TestContext>("rabbitmq")` with no policy configure callback; add global policy `BatchSize = 20` via `AddDeliveryPolicies(Action<>)`; resolve `DeliveryContext("rabbitmq", "send-pending", "TestContext")` and assert `policy.BatchSize == 20`

- [x] T012 [US2] Add unit test `Processor_Without_Any_Policy_Uses_Built_In_DefaultsAsync` in `core/test/Juice.EventBus.Tests/DeliveryPoliciesTest.cs`: register a processor with no policy and no global policy; resolve and assert policy matches `DeliveryPolicy.Default` values

**Checkpoint**: US2 verified — global fallback works, existing apps unaffected.

---

## Phase 5: User Story 3 — Processor Policy Overrides Global Partially (Priority: P3)

**Goal**: A processor code policy that sets only some fields inherits unset fields from the global policy. Developers do not need to repeat global settings.

**Independent Test**: Set global `BatchSize = 10` and `Interval = 5s`; register processor with code policy setting only `BatchSize = 50`; verify resolved policy has `BatchSize = 50` and `Interval = 5s`.

### Implementation for User Story 3

> No new implementation code required — the null-coalescing merge via `PolicyConfiguration.ToPolicy(globalDefault)` in T008 already handles partial override.

- [x] T013 [US3] Add unit test `Processor_Code_Policy_Partially_Overrides_Global_Inherits_Rest_Async` in `core/test/Juice.EventBus.Tests/DeliveryPoliciesTest.cs`: set global `DefaultPolicy { BatchSize = 10, Interval = 5s }`; register processor code policy `{ BatchSize = 50 }`; resolve and assert `policy.BatchSize == 50` and `policy.Interval == TimeSpan.FromSeconds(5)`

**Checkpoint**: US3 verified — partial override works correctly via null-coalescing.

---

## Phase 6: Polish & Cross-Cutting Concerns

- [x] T014 [P] Update `specs/009-processor-delivery-policy/research.md` Decision 4 to reflect the corrected resolution order from the `/speckit.clarify` session: global config-section `Policies` key matches (steps 1–4, existing) → processor code `DefaultPolicy` (new, step 5) → global `DefaultPolicy` (step 6) → built-in (step 7); remove the old "check processor at the very beginning" statement

- [x] T015 [P] Update `specs/009-processor-delivery-policy/data-model.md` resolution chain diagram to match the clarified spec: show the two-tier config-section vs. processor-code priority with the `DefaultPolicy` split

---

## Dependencies & Execution Order

### Phase Dependencies

- **Foundational (Phase 2)**: No dependencies — start immediately
- **US1 (Phase 3)**: Depends on Phase 2 (T001) ⚠️
- **US2 (Phase 4)**: Depends on Phase 3 complete (T001–T008)
- **US3 (Phase 5)**: Depends on Phase 3 complete (T001–T008)
- **Polish (Phase 6)**: Independent — can run any time after Phase 3

### User Story Dependencies

- **US1 (P1)**: Requires T001 (registry). Core implementation: T002 → T003 → T004 → T005/T006 [P] → T007 → T008 → T009/T010
- **US2 (P2)**: Requires T001–T008. No new implementation — tests only (T011, T012)
- **US3 (P3)**: Requires T001–T008. No new implementation — test only (T013)

### Parallel Opportunities Within US1

- T005 and T006 modify the same file (`DeliveryBuilder.cs`) — write both methods in one edit pass
- T007 and T008 modify the same file (`DeliveryPolicyConfiguration.cs`) — sequential; T008 depends on T007
- T009 and T010 are separate test methods in the same file — write in one edit pass

---

## Parallel Example: User Story 1

```
Sequential spine:           T001 → T002 → T003 → T004 → T007 → T008
                                                  ↓
Parallel branch (same file):            T005 + T006 (both in DeliveryBuilder.cs)
                                                  ↓
Test tasks (same file, one pass):       T009 + T010
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 2: T001 — `ProcessorPolicyRegistry`
2. Complete Phase 3: T002–T010 — API surface + resolver enhancement + US1 tests
3. **STOP and VALIDATE**: Call `AddDeliveryProcessor<T>("pub", proc => proc.AddDeliveryPolicies(...))` and verify policy resolution
4. Ship as MVP — US2 and US3 are validated by US1 infrastructure with zero extra code

### Incremental Delivery

1. T001 → Foundation
2. T002–T008 → Feature implementation (zero tests yet)
3. T009–T010 → US1 tests (confirm the feature works)
4. T011–T012 → US2 tests (confirm backward compatibility)
5. T013 → US3 test (confirm partial override)
6. T014–T015 → Spec artifacts updated

### Notes

- US2 and US3 require **no new implementation code** — only new test cases. The implementation from Phase 3 already satisfies all three user stories.
- T005 and T006 guard with `if (!registry.TryAdd(key)) return this;` already present — `RegisterProcessorPolicies` is idempotent (processor already guarded by `DeliveryProcessorRegistry`).
- All tests follow the existing `DeliveryPoliciesTest` pattern: in-memory service collection + `BuildServiceProvider()` + `GetRequiredService<IDeliveryPolicyResolver>()`.
- Tests must use `Async` suffix per codebase convention (see CLAUDE.md).
