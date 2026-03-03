# Tasks: Policy-Controlled Routing Key

**Input**: Design documents from `/specs/002-policy-routing-key/`
**Branch**: `002-policy-routing-key` | **Generated**: 2026-03-03
**Prerequisites**: plan.md ✅ | spec.md ✅ | research.md ✅ | data-model.md ✅ | contracts/public-api.md ✅ | quickstart.md ✅

**Tests**: Not explicitly requested. Three targeted unit tests are included because they are the only way to independently validate each user story and they exercise logic internal to `DefaultEventPublishingPolicy` (not accessible via end-to-end integration tests alone).

**Organization**: Phase 2 establishes the foundational `PublishRoute` type change that all user stories depend on. Phase 3 wires the routing key end-to-end (US1 and its tests). Phase 4 verifies backward compat (US2, test-only). Phase 5 verifies context-driven routing (US3, test-only). Phase 6 validates the full build and quickstart.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (US1, US2, US3)
- All paths relative to repository root

---

## Phase 1: Setup

**Purpose**: Confirm no new projects are needed and the existing solution compiles before changes begin.

- [x] T001 Verify `dotnet build Juice.sln` passes with zero errors on the baseline branch before any changes (no files modified — this is a pre-flight check)

**Checkpoint**: Solution compiles clean; implementation can begin.

---

## Phase 2: Foundational (Blocking Prerequisite)

**Purpose**: Extend the `PublishRoute` type with the optional `RoutingKey` parameter. This single change is required by all three user stories and all downstream implementation tasks.

**⚠️ CRITICAL**: No user story work can begin until this phase is complete.

- [x] T002 Extend `core/src/Juice.Messaging/Policies/PublishRoute.cs`: add `string? RoutingKey = null` as an optional third positional parameter to the `PublishRoute` record — record becomes `public sealed record PublishRoute(string PublisherKey, string Destination, string? RoutingKey = null);` — all existing callsites compile unchanged

**Checkpoint**: `dotnet build core/src/Juice.Messaging/` succeeds; `PublishRoute` has three parameters; existing `new PublishRoute("key", "dest")` calls compile.

---

## Phase 3: User Story 1 — Configure a Fixed Routing Key per Event Type (Priority: P1) 🎯 MVP

**Goal**: Wire the optional routing key from policy configuration through the full publish pipeline so a message is delivered to the broker with the policy-specified routing key instead of the event's class name.

**Independent Test**: Configure `PublisherDestination.RoutingKey = "orders.placed"` in a `PublishingPolicyOptions`, resolve a route via `DefaultEventPublishingPolicy`, and assert `route.RoutingKey == "orders.placed"`. Separately, assert that `RabbitMQProducer` would select `x-routing-key` header over `x-message-name` by verifying the derivation chain order.

### Implementation for User Story 1

- [x] T003 [P] [US1] Add `public string? RoutingKey { get; init; }` property to `PublisherDestination` in `core/src/Juice.Messaging/Policies/Internal/PublishingPolicyOptions.cs` — no other changes in this file
- [x] T004 [P] [US1] Update `DefaultEventPublishingPolicy.Map()` in `core/src/Juice.Messaging/Policies/Internal/DefaultEventPublishingPolicy.cs`: change the LINQ select from `new PublishRoute(p.Key, p.Destination)` to `new PublishRoute(p.Key, p.Destination, p.RoutingKey)` so the routing key is propagated from config into the resolved route (depends on T002 for `PublishRoute`'s third parameter, and T003 for `PublisherDestination.RoutingKey`)
- [x] T005 [P] [US1] Update `CompositeEventPublisher.PublishAsync` in `core/src/Juice.EventBus/Internal/CompositeEventPublisher.cs`: in the `foreach (var route in routes)` loop, add `if (!string.IsNullOrEmpty(route.RoutingKey)) { headers["x-routing-key"] = route.RoutingKey; }` to the headers dictionary built inside the private `PublishAsync<T>` overload — thread `route.RoutingKey` from the outer loop to the private method (depends on T002)
- [x] T006 [P] [US1] Update `RabbitMQProducer.PublishAsync` in `core/src/Juice.EventBus.RabbitMQ/Publishing/RabbitMQProducer.cs`: prepend `headers.GetHeaderString("x-routing-key") ??` to the existing routing key derivation chain so it reads: `var routingKey = headers.GetHeaderString("x-routing-key") ?? headers.GetHeaderString("x-message-name") ?? headers.GetHeaderString("x-message-type") ?? throw new InvalidOperationException(...);` — this task is independent and can run in parallel with T003/T004/T005

### Tests for User Story 1

- [x] T007 [P] [US1] Add `[Fact]` test `Should_Include_RoutingKey_In_Resolved_Route_When_Configured` to `core/test/Juice.EventBus.Tests/PublishPoliciesTest.cs`: create a `PublishingPolicyOptions` with a `PublishRule` whose `PublisherDestination` has `RoutingKey = "orders.placed"`, resolve via `DefaultEventPublishingPolicy`, assert `routes.First().RoutingKey == "orders.placed"` (depends on T003 and T004)

**Checkpoint**: `dotnet build Juice.sln` passes; the new test in `PublishPoliciesTest` passes; routing key set in `PublisherDestination` flows into `PublishRoute.RoutingKey`; `x-routing-key` header is set in `CompositeEventPublisher`; `RabbitMQProducer` checks `x-routing-key` first.

---

## Phase 4: User Story 2 — Backward Compatibility: Existing Policies Work Unchanged (Priority: P1)

**Goal**: Confirm that existing policy configurations that omit `RoutingKey` continue to produce `PublishRoute.RoutingKey == null`, and that `RabbitMQProducer` falls through to the `x-message-name` derivation unchanged.

**Independent Test**: Run the existing `PublishPoliciesTest` suite with no modification — all 11 pre-existing tests must pass. Add one new test asserting that a rule without `RoutingKey` yields `null` on the resolved route.

### Implementation for User Story 2

*(No implementation changes needed — US2 is validated by the existing test suite plus one regression test.)*

### Tests for User Story 2

- [x] T008 [P] [US2] Add `[Fact]` test `Should_Return_Null_RoutingKey_When_Not_Configured` to `core/test/Juice.EventBus.Tests/PublishPoliciesTest.cs`: use the existing `PublisherDestination { Key = "rabbitmq", Destination = "default_exchange" }` pattern (no `RoutingKey` set), resolve via `DefaultEventPublishingPolicy`, assert `routes.First().RoutingKey == null` — confirms null default and no regression (depends on T004)
- [x] T009 [US2] Run `dotnet test core/test/Juice.EventBus.Tests/ --filter "PublishPoliciesTest"` and confirm all pre-existing tests plus T007 and T008 pass without modification to any existing test

**Checkpoint**: All 13+ `PublishPoliciesTest` tests pass. Backward compat confirmed.

---

## Phase 5: User Story 3 — Context-Driven Routing Key Variation (Priority: P2)

**Goal**: Demonstrate that a policy rule can produce different routing keys based on event context (domain, tenant, tier) — the `PolicyResolveContext` already carries all required fields; no infrastructure changes needed beyond Phase 3.

**Independent Test**: Add one test demonstrating that two rules for the same event type produce different routing keys when matched by tenant identifier.

### Implementation for User Story 3

*(No implementation changes needed — `PolicyResolveContext` already exposes all context fields; a custom `IMessagePublishingPolicy` can read them and set `RoutingKey` on the returned `PublishRoute`. The infrastructure wired in Phase 3 handles the rest.)*

### Tests for User Story 3

- [x] T010 [P] [US3] Add `[Fact]` test `Should_Use_Different_RoutingKeys_For_Different_Tenants` to `core/test/Juice.EventBus.Tests/PublishPoliciesTest.cs`: define two rules for `OrderPlacedEvent` — one matching `TenantIdentifier = "acme"` with `RoutingKey = "acme.orders.placed"` and one matching `TenantIdentifier = "globex"` with `RoutingKey = "globex.orders.placed"` — resolve both contexts and assert each produces the correct tenant-prefixed routing key (depends on T004)

**Checkpoint**: Context-driven routing key variation is demonstrated and passes.

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Final build validation, full test suite confirmation, and quickstart scenario verification.

- [x] T011 [P] Run `dotnet build Juice.sln` from repository root and confirm zero errors and no unexpected warnings — confirms all changes integrate cleanly across the solution
- [x] T012 [P] Run `dotnet test core/test/Juice.EventBus.Tests/` and confirm all tests pass including the three new routing-key tests (T007, T008, T010)
- [x] T013 Validate `specs/002-policy-routing-key/quickstart.md` checklist: confirm the `appsettings.json` configuration snippet (adding `RoutingKey` to a `PublisherDestination`) is accepted by `PublishingPolicyOptions` deserialization and that `dotnet build` succeeds on a project referencing the modified libraries

---

## Dependencies & Execution Order

### Phase Dependencies

- **Phase 1 (Setup)**: No dependencies — start immediately
- **Phase 2 (Foundational)**: Depends on Phase 1 (T001 confirms baseline)
- **Phase 3 (US1)**: Depends on Phase 2 complete — all tasks read from `PublishRoute.RoutingKey`
- **Phase 4 (US2)**: Depends on Phase 3 complete — tests require `DefaultEventPublishingPolicy` to propagate `RoutingKey`
- **Phase 5 (US3)**: Depends on Phase 3 complete — test requires the same policy wiring
- **Phase 6 (Polish)**: Depends on Phases 3–5 complete

### User Story Dependencies

- **US1 (P1)**: Unblocked after Phase 2 — creates the routing key pipeline
- **US2 (P1)**: Blocked by US1 — tests the same pipeline's null/default behaviour
- **US3 (P2)**: Blocked by US1 — tests the same pipeline with context-varied rules

### Within Each Phase

- T003 must complete before T004 (`DefaultEventPublishingPolicy.Map()` uses `PublisherDestination.RoutingKey`)
- T002 must complete before T003, T004 (foundational `PublishRoute` type change)
- T006 is independent of T003/T004/T005 (different file, no data dependency)
- T007 and T008 and T010 are all independent of each other ([P] — same file, different test methods, can be written concurrently)
- T009 depends on T007 and T008 (runs them)
- T011 and T012 can run in parallel

### Parallel Opportunities

**Phase 3 (US1)**: After T002, T003 + T005 + T006 can all run in parallel (separate files). T004 is blocked only by T003.

```bash
# After T002 (PublishRoute extended), launch in parallel:
Task: "Add RoutingKey to PublisherDestination in PublishingPolicyOptions.cs"   # T003
Task: "Check x-routing-key in RabbitMQProducer.cs derivation chain"           # T006
# Then, once T003 is done:
Task: "Update DefaultEventPublishingPolicy.Map() in DefaultEventPublishingPolicy.cs"  # T004
Task: "Set x-routing-key header in CompositeEventPublisher.cs"                 # T005
```

**Phase 4–5**: T008 and T010 can be written in parallel with T007 (all different test methods in the same file — treat as separate edits).

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup (T001)
2. Complete Phase 2: Foundational (T002)
3. Complete Phase 3: US1 (T003–T007)
4. **STOP and VALIDATE**: `dotnet test --filter "Should_Include_RoutingKey"` passes; custom `RoutingKey` in config flows to RabbitMQ

### Incremental Delivery

1. Phase 1 + 2 → `PublishRoute` extended (no visible behaviour change)
2. Phase 3 → US1 complete (full pipeline wired, custom routing key works)
3. Phase 4 → US2 confirmed (regression-free, existing policies unchanged)
4. Phase 5 → US3 demonstrated (context-driven routing keys verified)
5. Phase 6 → Final build and test validation

### Single-Developer Sequential Strategy

```
T001 → T002 → T003 + T005 + T006 (parallel) → T004 → T007 + T008 + T010 (parallel) → T009 → T011 + T012 (parallel) → T013
```

---

## Notes

- **No new projects** — changes are entirely within existing `core/src/` libraries and the existing `core/test/Juice.EventBus.Tests/` project
- **No DB migrations** — routing key travels as a message header; persisted in the existing outbox `Headers` JSON column
- **CS0618 warnings**: none expected — no deprecated types are touched
- **`x-routing-key` header**: set only when `route.RoutingKey` is non-null/non-empty; null or empty string is treated as absent (FR-007)
- **Outbox path covered automatically**: `DeliveryProcessor` already passes `OutboxEvent.Headers` through `PublishContext.Headers` to `RabbitMQProducer`; the persisted `x-routing-key` header is therefore honoured on outbox delivery with no additional changes
- [P] tasks = different files, no shared state dependencies
- Commit after each phase checkpoint for easy rollback if needed
