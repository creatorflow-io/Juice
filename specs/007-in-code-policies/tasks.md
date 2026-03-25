# Tasks: In-Code Publishing Policies

**Input**: Design documents from `/specs/007-in-code-policies/`
**Prerequisites**: plan.md ✅, spec.md ✅, research.md ✅, data-model.md ✅, contracts/api.md ✅, quickstart.md ✅

**Tests**: Unit tests included — no infrastructure required (no RabbitMQ, no database).

**Organization**: Tasks grouped by user story. All changes are in `core/src/Juice.Messaging/` and `core/test/Juice.Messaging.Tests/`. No new project created.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no shared dependencies)
- **[Story]**: Which user story this task belongs to
- All paths are relative to the repository root

---

## Phase 1: Setup (File Stubs)

**Purpose**: Create new files with empty skeletons so all phases can reference them immediately.

- [x] T001 Create `core/src/Juice.Messaging/Policies/PublishingPolicyBuilder.cs` with empty `PublishingPolicyBuilder` and `PublishRuleBuilder` class stubs (correct namespace: `Juice.Messaging.Policies`, no implementation yet)
- [x] T002 [P] Create `core/test/Juice.Messaging.Tests/PublishingPolicyBuilderTest.cs` with an empty xUnit test class stub (`PublishingPolicyBuilderTest`, no test methods yet, correct usings for `Juice.Messaging.Policies`, `Microsoft.Extensions.DependencyInjection`)

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Core data model changes that all user stories depend on — rule tiebreaker logic must exist before any builder or merge scenario can be tested.

**⚠️ CRITICAL**: Phases 3–5 depend on these changes being in place.

- [x] T003 Add `bool IsCodeDefined { get; init; }` property (default `false`) to the internal `PublishRule` record in `core/src/Juice.Messaging/Policies/Internal/PublishingPolicyOptions.cs`
- [x] T004 [P] Update `DefaultEventPublishingPolicy.ResolveAsync` in `core/src/Juice.Messaging/Policies/Internal/DefaultEventPublishingPolicy.cs` to sort rules by `OrderByDescending(r => r.Priority).ThenByDescending(r => r.IsCodeDefined ? 1 : 0)` instead of `OrderByDescending(r => r.Priority)` alone

**Checkpoint**: `IsCodeDefined` exists and tiebreaker sort is active — ready for builder implementation.

---

## Phase 3: User Story 1 — Define All Publishing Rules Programmatically (Priority: P1) 🎯 MVP

**Goal**: Developers can configure a complete publishing policy (default route + match rules) entirely in C# startup code with no `appsettings.json` dependency.

**Independent Test**: Construct a `ServiceCollection`, call `AddPublishingPolicies(Action<PublishingPolicyBuilder>)` with a default and one rule, build the provider, resolve `IMessagePublishingPolicy`, and assert that `ResolveAsync` returns the expected route.

### Implementation for User Story 1

- [x] T005 [US1] Implement `PublishRuleBuilder` in `core/src/Juice.Messaging/Policies/PublishingPolicyBuilder.cs` with chainable methods: `ForEvent(string)`, `ForDomain(string)`, `ForTenant(string)`, `ForTenantTier(string)`, `PublishTo(string publisherKey, string destination, string? routingKey = null)` — each `ForXxx` method sets the corresponding match field; `PublishTo` appends to an internal publishers list
- [x] T006 [US1] Implement `PublishingPolicyBuilder` in `core/src/Juice.Messaging/Policies/PublishingPolicyBuilder.cs` with: `SetDefault(string publisherKey, string destination, string? routingKey = null)` → stores a single default `PublisherDestination`; `AddRule(int priority, Action<PublishRuleBuilder> configure)` → creates a `PublishRuleBuilder`, invokes the delegate, and appends the resulting `PublishRule` (with `IsCodeDefined = true`) to an internal list
- [x] T007 [US1] Add `internal void ApplyTo(PublishingPolicyOptions options)` to `PublishingPolicyBuilder` in `core/src/Juice.Messaging/Policies/PublishingPolicyBuilder.cs` — appends all code-defined rules to `options.Rules`; if a default was set via `SetDefault`, appends to `options.Default.Publishers`
- [x] T008 [US1] Add `AddPublishingPolicies(Action<PublishingPolicyBuilder> configure)` overload to `MessagingBuilder` in `core/src/Juice.Messaging/MessagingBuilder.cs` — creates a `PublishingPolicyBuilder`, invokes `configure`, calls `Services.Configure<PublishingPolicyOptions>(opts => builder.ApplyTo(opts))`, then `Services.TryAddSingleton<IMessagePublishingPolicy, DefaultEventPublishingPolicy>()`
- [x] T009 [P] [US1] Write unit tests in `core/test/Juice.Messaging.Tests/PublishingPolicyBuilderTest.cs`: (a) code-only policy with default and one domain rule resolves correct destination; (b) code-only policy with no matching rule falls back to default; (c) code-defined rule with routing key propagates to `PublishRoute.RoutingKey`

**Checkpoint**: User Story 1 independently testable — `AddPublishingPolicies(delegate)` with no JSON config produces correct routes.

---

## Phase 4: User Story 2 — Mix Code-Defined Rules with Config-Driven Rules (Priority: P1)

**Goal**: Code-defined rules and JSON config rules coexist in a single deployment; the higher-priority rule always wins; code rules win on equal priority.

**Independent Test**: Register one rule via `AddPublishingPolicies(configSection)` and a different rule via `AddPublishingPolicies(codeDelegate)`; verify each event routes to its respective publisher; verify that at equal priority the code rule wins.

### Implementation for User Story 2

- [x] T010 [US2] Update the existing `AddPublishingPolicies(IConfigurationSection policies)` overload in `core/src/Juice.Messaging/MessagingBuilder.cs` to replace the manual `Services.Any(sd => sd.ServiceType == typeof(IMessagePublishingPolicy))` guard with `Services.TryAddSingleton<IMessagePublishingPolicy, DefaultEventPublishingPolicy>()` — this allows the two overloads to coexist (each independently registers options; only one `DefaultEventPublishingPolicy` is ever registered)
- [x] T011 [P] [US2] Write unit tests in `core/test/Juice.Messaging.Tests/PublishingPolicyBuilderTest.cs`: (a) both `AddPublishingPolicies(section)` and `AddPublishingPolicies(delegate)` called — event matching code rule routes to code destination; (b) event matching config rule routes to config destination; (c) code rule and config rule at the same priority — code rule wins; (d) config rule at higher priority than code rule — config rule wins

**Checkpoint**: User Stories 1 and 2 independently testable — code+config coexistence and tiebreaker verified.

---

## Phase 5: User Story 3 — Register a Fully Custom Policy Implementation (Priority: P2)

**Goal**: Developers with complex routing logic can register their own `IMessagePublishingPolicy` implementation through `MessagingBuilder`; `DefaultEventPublishingPolicy` is not registered.

**Independent Test**: Implement a minimal `IMessagePublishingPolicy` that always returns a fixed route; register it via `AddPublishingPolicies<TCustomPolicy>()`; resolve `IMessagePublishingPolicy` and verify the instance is `TCustomPolicy`, not `DefaultEventPublishingPolicy`.

### Implementation for User Story 3

- [x] T012 [US3] Add `AddPublishingPolicies<TPolicy>() where TPolicy : class, IMessagePublishingPolicy` overload to `MessagingBuilder` in `core/src/Juice.Messaging/MessagingBuilder.cs` — calls `Services.TryAddSingleton<IMessagePublishingPolicy, TPolicy>()` only; does not touch `PublishingPolicyOptions`
- [x] T013 [P] [US3] Write unit tests in `core/test/Juice.Messaging.Tests/PublishingPolicyBuilderTest.cs`: (a) custom policy registered via generic overload resolves to the custom type; (b) calling `AddPublishingPolicies(configSection)` after `AddPublishingPolicies<TCustomPolicy>()` does not replace the custom policy (first registration wins); (c) custom policy `ResolveAsync` is invoked for published events

**Checkpoint**: All three user stories independently testable and complete.

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Documentation, backward-compatibility verification, and quickstart validation.

- [x] T014 Add XML `<summary>` documentation to the public members of `PublishingPolicyBuilder` and `PublishRuleBuilder` in `core/src/Juice.Messaging/Policies/PublishingPolicyBuilder.cs`, and to both new overloads in `core/src/Juice.Messaging/MessagingBuilder.cs` (mirror descriptions from `contracts/api.md`)
- [x] T015 [P] Verify backward compatibility: confirm the existing `AddTestMessaging` helper in `core/test/Juice.EventBus.Tests/DependencyInjection/EventBustTestServiceCollectionExtensions.cs` compiles and behaves identically after T010's guard change (no call-site modifications required)
- [x] T016 [P] Run the quickstart unit test scenario from `specs/007-in-code-policies/quickstart.md` (Scenario A — code-only policy) as a manual smoke test to confirm the end-to-end DI resolution path works in `core/test/Juice.Messaging.Tests/PublishingPolicyBuilderTest.cs`

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — start immediately; T001 and T002 run in parallel
- **Foundational (Phase 2)**: Depends on Phase 1 completion; T003 and T004 run in parallel
- **US1 (Phase 3)**: Depends on Phase 2 — T005 → T006 → T007 → T008 (sequential within file); T009 in parallel once T008 is done
- **US2 (Phase 4)**: Depends on Phase 3 being complete; T010 modifies `MessagingBuilder.cs` (same file as T008 — do not overlap); T011 in parallel once T010 is done
- **US3 (Phase 5)**: Depends on Phase 2 only (not on US1/US2); T012 can start after T010 completes (same file); T013 in parallel once T012 is done
- **Polish (Phase 6)**: Depends on all user story phases being complete

### User Story Dependencies

- **US1 (P1)**: Requires Phase 2 only — no dependency on US2 or US3
- **US2 (P1)**: Requires US1 complete (shares `MessagingBuilder.cs` file and tests file; tests build on US1 assertions)
- **US3 (P2)**: Requires Phase 2 only (adds a new overload to `MessagingBuilder.cs` — coordinate with US2's T010 to avoid file conflicts)

### Within Each User Story

- `PublishRuleBuilder` (T005) before `PublishingPolicyBuilder` (T006) — builder uses `PublishRuleBuilder`
- `ApplyTo` (T007) before `MessagingBuilder` overload (T008) — overload calls `ApplyTo`
- Both T003 and T004 before any builder work (T005+) — tiebreaker must exist before tests run

---

## Parallel Opportunities

### Phase 1 (can start together)
```
T001: Create PublishingPolicyBuilder.cs stub
T002: Create PublishingPolicyBuilderTest.cs stub
```

### Phase 2 (after Phase 1, run together)
```
T003: Add IsCodeDefined to PublishRule (PublishingPolicyOptions.cs)
T004: Add tiebreaker sort (DefaultEventPublishingPolicy.cs)
```

### Phase 3 — sequential within the builder file, then parallel test
```
Sequential: T005 → T006 → T007 → T008
Parallel after T008: T009 (tests, separate file)
```

### Phases 4+5 — coordinate MessagingBuilder.cs edits
```
T010 completes first (guards change in MessagingBuilder.cs)
Then T012 (new overload in same file)
T011 and T013 (tests) can run in parallel once their respective impl tasks are done
```

---

## Implementation Strategy

### MVP (User Story 1 Only)

1. Complete Phase 1: Create file stubs (T001, T002 — parallel)
2. Complete Phase 2: Add `IsCodeDefined` + tiebreaker (T003, T004 — parallel)
3. Complete Phase 3: `PublishRuleBuilder` → `PublishingPolicyBuilder` → `ApplyTo` → `MessagingBuilder` overload → tests (T005–T009)
4. **STOP and VALIDATE**: Run `core/test/Juice.Messaging.Tests/` — all US1 tests pass, zero regressions
5. Demo: code-only policy routing works end-to-end

### Incremental Delivery

1. Setup + Foundational (Phases 1–2) → infrastructure ready
2. US1 (Phase 3) → code-only policies work ✅ **MVP**
3. US2 (Phase 4) → code+config coexistence works ✅
4. US3 (Phase 5) → custom policy registration works ✅
5. Polish (Phase 6) → documentation + backward-compat verified ✅

---

## Notes

- T003 and T004 are in different files — safe to run in parallel
- T005–T008 are all in `PublishingPolicyBuilder.cs` — run sequentially within one session
- T008, T010, T012 all touch `MessagingBuilder.cs` — do not overlap; run in order
- T009, T011, T013, T016 all touch `PublishingPolicyBuilderTest.cs` — run sequentially or use append-only edits
- No `[InitializeMessageContext]` needed — these are pure DI/options unit tests with no outbox or event-bus flow
- No `IgnoreOnCIFact` needed — no RabbitMQ or database dependency
- `PublishRuleMatch.IsMatch` is `internal` but visible via `InternalsVisibleTo("Juice.EventBus.Tests")` — the test project `Juice.Messaging.Tests` does NOT have `InternalsVisibleTo`; all assertions should go through the public `IMessagePublishingPolicy` interface, not internal types
