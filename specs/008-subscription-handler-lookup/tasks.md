# Tasks: Subscriptions Manager Handler Lookup for Local Routes

**Input**: Design documents from `/specs/008-subscription-handler-lookup/`
**Prerequisites**: plan.md ✅, spec.md ✅, research.md ✅, data-model.md ✅, contracts/public-api.md ✅, quickstart.md ✅

**Organization**: Tasks are grouped by user story to enable independent implementation and testing of each story.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (US1, US2, US3)
- Paths are relative to repository root

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Confirm test infrastructure and familiarize with the existing subscription machinery before making changes.

- [x] T001 Read `core/src/Juice.EventBus/Subscriptions/InMemorySubscriptionsManager.cs` and verify `internal` visibility — determine if `InternalsVisibleTo` for `Juice.Messaging.Local` is needed
- [x] T002 [P] Read `core/test/Juice.Messaging.Local.Tests/Juice.Messaging.Local.Tests.csproj` to confirm project references and verify `Juice.EventBus` is already referenced
- [x] T003 [P] Read `core/src/Juice.Messaging.Local/Juice.Messaging.Local.csproj` to confirm project references for `Juice.EventBus` (needed for `ISubscriptionsManager`, `InMemorySubscriptionsManager`)

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Implement `LocalConsumerBuilder` and `AddLocalConsumer` — these are required by ALL user stories because they are what populates the keyed `ISubscriptionsManager` that US2 and US3 consume.

**⚠️ CRITICAL**: US2 and US3 cannot be implemented until this phase is complete.

- [x] T004 Create `core/src/Juice.Messaging.Local/Internal/LocalConsumerBuilder.cs` — `internal sealed` class with `IServiceCollection _services`, `List<SubscriptionInfo> _subscriptions`, `Subscribe<TEvent, THandler>(string? key = null)` method that calls `services.TryAddTransient<THandler>()` and accumulates a `SubscriptionInfo.Typed(typeof(TEvent), typeof(THandler), key)`, and `Build()` returning `new InMemorySubscriptionsProvider(_subscriptions)`
- [x] T005 Add `AddLocalConsumer(this MessagingBuilder builder, Action<LocalConsumerBuilder> configure)` method to `core/src/Juice.Messaging.Local/DependencyInjection/LocalMessagingBuilderExtensions.cs` — creates `LocalConsumerBuilder(builder.Services)`, calls `configure(consumerBuilder)`, registers the resulting `ISubscriptionsProvider` via `builder.Services.AddSingleton<ISubscriptionsProvider>(provider)`, then calls `builder.Services.TryAddKeyedSingleton<ISubscriptionsManager>("local", (sp, _) => new InMemorySubscriptionsManager(sp.GetServices<ISubscriptionsProvider>(), sp.GetRequiredService<ILogger<InMemorySubscriptionsManager>>(), topicSupport: false))`

**Checkpoint**: After T004–T005, calling `AddLocalConsumer(c => c.Subscribe<TEvent, THandler>())` registers a keyed `ISubscriptionsManager` with the subscription recorded.

---

## Phase 3: User Story 1 — Register Local Handler via Subscriptions Manager (Priority: P1) 🎯 MVP

**Goal**: After calling `AddLocalConsumer`, the keyed `ISubscriptionsManager` (key `"local"`) returns the registered handler types when queried by event name.

**Independent Test**: Build a minimal service collection with `AddLocalConsumer(c => c.Subscribe<TestEvent, TestHandler>())`, resolve `ISubscriptionsManager` (key `"local"`), call `GetHandlersForEventAsync("TestEvent")`, assert `TestHandler` is returned.

### Implementation for User Story 1

- [x] T006 [US1] Verify `InMemorySubscriptionsManager` constructor is accessible from `Juice.Messaging.Local` — if it is `internal`, add `[assembly: InternalsVisibleTo("Juice.Messaging.Local")]` to `core/src/Juice.EventBus/Assembly.cs` (or create that file if absent), mirroring how other projects in the solution expose internals
- [x] T007 [P] [US1] Write unit test `Subscribe_registers_handler_in_subscriptions_manager_Async` in `core/test/Juice.Messaging.Local.Tests/LocalConsumerBuilderTests.cs` — builds `ServiceCollection`, calls `AddLocalConsumer(c => c.Subscribe<OrderCreatedEvent, OrderCreatedHandler>())`, resolves `ISubscriptionsManager` keyed `"local"`, asserts `GetHandlersForEventAsync("OrderCreatedEvent")` returns `typeof(OrderCreatedHandler)`
- [x] T008 [P] [US1] Write unit test `Subscribe_is_idempotent_for_same_event_handler_pair_Async` in `core/test/Juice.Messaging.Local.Tests/LocalConsumerBuilderTests.cs` — calls `Subscribe<TEvent, THandler>()` twice on the same builder, asserts handler type appears exactly once in `GetHandlersForEventAsync`
- [x] T009 [P] [US1] Write unit test `Multiple_handlers_for_same_event_are_all_returned_Async` in `core/test/Juice.Messaging.Local.Tests/LocalConsumerBuilderTests.cs` — registers two handlers for the same event, asserts both types are returned
- [x] T010 [P] [US1] Write unit test `Multiple_AddLocalConsumer_calls_are_additive_Async` in `core/test/Juice.Messaging.Local.Tests/LocalConsumerBuilderTests.cs` — calls `AddLocalConsumer` twice on the same `MessagingBuilder`, asserts handlers from both calls are returned
- [x] T011 [US1] Write unit test `No_handlers_registered_returns_empty_not_throws_Async` in `core/test/Juice.Messaging.Local.Tests/LocalConsumerBuilderTests.cs` — calls `AddLocalConsumer` with no subscriptions, asserts `GetHandlersForEventAsync("UnknownEvent")` returns empty without throwing

**Checkpoint**: `LocalConsumerBuilderTests` all pass. Developer can register handlers and query the registry independently of dispatch.

---

## Phase 4: User Story 2 — Dispatch via Subscriptions Manager on Local-Channel Route (Priority: P2)

**Goal**: When `AddLocalConsumer` has been called, publishing an event on `"local-channel"` invokes only the handlers registered in the subscriptions manager — not all handlers in DI. When `AddLocalConsumer` was NOT called, the existing DI-scan fallback runs unchanged.

**Independent Test**: Register `TestHandler` via `AddLocalConsumer`, publish a `TestEvent` to the `"local-channel"` channel writer, wait for the channel to drain, assert `TestHandler.HandleAsync` was called. Then assert a second handler in DI (but not in the subscriptions manager) was NOT called.

### Implementation for User Story 2

- [x] T012 [US2] Modify `LocalDispatchHelper.DispatchIntegrationEventAsync` in `core/src/Juice.Messaging.Local/Internal/LocalDispatchHelper.cs` — add `ISubscriptionsManager? subscriptionsManager` as the third parameter (before `IIntegrationEvent evt`); replace the unconditional `serviceProvider.GetServices(handlerType)` DI scan with: if `subscriptionsManager != null` → `await subscriptionsManager.GetHandlersForEventAsync(evt.GetType().Name)` → `handlerTypes`; else → existing DI scan fallback
- [x] T013 [US2] Modify `LocalChannelBackgroundService` constructor in `core/src/Juice.Messaging.Local/Internal/LocalChannelBackgroundService.cs` — add `ISubscriptionsManager? subscriptionsManager = null` parameter (use `[FromKeyedServices("local")]` attribute on .NET 8+; for net6 resolve via `sp.GetKeyedService<ISubscriptionsManager>("local")` in constructor body from an injected `IServiceProvider`), store in `_subscriptionsManager` field; update the `LocalDispatchHelper.DispatchIntegrationEventAsync(sp, dispatcher, integrationEvent, ...)` call to pass `_subscriptionsManager` as the new third argument
- [x] T014 [P] [US2] Write integration test `Local_channel_dispatches_to_registered_handler_via_subscriptions_manager_Async` in `core/test/Juice.Messaging.Local.Tests/LocalChannelTests.cs` — sets up host with `AddLocalChannel()` + `AddLocalConsumer(c => c.Subscribe<TestEvent, SpyHandler>())`, writes event to channel, waits for dispatch, asserts `SpyHandler` was invoked
- [x] T015 [P] [US2] Write integration test `Local_channel_does_not_invoke_unregistered_handler_when_manager_present_Async` in `core/test/Juice.Messaging.Local.Tests/LocalChannelTests.cs` — registers `SpyHandler` in DI but NOT via `AddLocalConsumer`, publishes event, asserts `SpyHandler` was NOT invoked (dispatch result is `NotHandled`)
- [x] T016 [P] [US2] Write integration test `Local_channel_fallback_to_di_scan_when_no_manager_Async` in `core/test/Juice.Messaging.Local.Tests/LocalChannelTests.cs` — sets up host with `AddLocalChannel()` only (no `AddLocalConsumer`), registers `SpyHandler` directly in DI, publishes event, asserts `SpyHandler` WAS invoked (fallback path)
- [x] T017 [P] [US2] Write integration test `Local_channel_returns_not_handled_when_no_handlers_in_manager_Async` in `core/test/Juice.Messaging.Local.Tests/LocalChannelTests.cs` — registers no handlers in manager for the published event type, asserts dispatch result is `NotHandled` and no exception is raised

**Checkpoint**: `LocalChannelTests` pass. Publishing on `"local-channel"` uses subscriptions manager when registered; falls back to DI scan otherwise.

---

## Phase 5: User Story 3 — Dispatch via Subscriptions Manager on Local (Outbox-Backed) Route (Priority: P3)

**Goal**: When `AddLocalConsumer` has been called, `LocalTransportPublisher` (invoked by `DeliveryHostedService` for `PublisherKey = "local"`) also uses the subscriptions manager for handler resolution — identical dispatch logic to the local-channel path.

**Independent Test**: Register `TestHandler` via `AddLocalConsumer`, save an outbox record for `PublisherKey = "local"`, let `DeliveryHostedService` process it via `LocalTransportPublisher`, assert `TestHandler.HandleAsync` was called.

### Implementation for User Story 3

- [x] T018 [US3] Modify `LocalTransportPublisher` constructor in `core/src/Juice.Messaging.Local/Internal/LocalTransportPublisher.cs` — add `ISubscriptionsManager? subscriptionsManager = null` parameter (same keyed injection pattern as T013), store in `_subscriptionsManager` field; update the `LocalDispatchHelper.DispatchIntegrationEventAsync(scope.ServiceProvider, _dispatcher, integrationEvent, ...)` call to pass `_subscriptionsManager` as the new third argument
- [x] T019 [P] [US3] Write integration test `Local_transport_dispatches_to_registered_handler_via_subscriptions_manager_Async` in `core/test/Juice.Messaging.Local.Tests/LocalTransportPublisherTests.cs` — instantiates `LocalTransportPublisher` with a mock/stub `ISubscriptionsManager` that returns `typeof(SpyHandler)` for the test event name, calls `PublishAsync` with a serialized event payload, asserts `SpyHandler` was invoked
- [x] T020 [P] [US3] Write integration test `Local_transport_fallback_to_di_scan_when_no_manager_Async` in `core/test/Juice.Messaging.Local.Tests/LocalTransportPublisherTests.cs` — instantiates `LocalTransportPublisher` with `subscriptionsManager: null`, registers `SpyHandler` in DI, calls `PublishAsync`, asserts `SpyHandler` WAS invoked (fallback)
- [x] T021 [P] [US3] Write integration test `Local_transport_idempotency_dedup_respected_Async` in `core/test/Juice.Messaging.Local.Tests/LocalTransportPublisherTests.cs` — calls `PublishAsync` twice with the same `MessageId`/source headers, asserts handler invoked once and second delivery returns `Duplicated`

**Checkpoint**: `LocalTransportPublisherTests` pass. All three delivery routes use subscriptions manager consistently.

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Documentation, backward-compat validation, and xml doc comments.

- [x] T022 [P] Add XML doc comment to `AddLocalConsumer` in `core/src/Juice.Messaging.Local/DependencyInjection/LocalMessagingBuilderExtensions.cs` — describe purpose, keyed manager key `"local"`, idempotent-call behavior, and backward-compat fallback
- [x] T023 [P] Add XML doc comment to `LocalConsumerBuilder` class and its `Subscribe` method in `core/src/Juice.Messaging.Local/Internal/LocalConsumerBuilder.cs` — describe DI registration side-effect and idempotency
- [x] T024 Run the quickstart.md test scenario end-to-end to validate the full developer experience: set up host per `quickstart.md`, publish `OrderCreatedEvent`, assert `OrderCreatedHandler` invoked — capture result in a comment in `core/test/Juice.Messaging.Local.Tests/LocalChannelTests.cs`
- [x] T025 Verify no existing tests in `core/test/Juice.Messaging.Local.Tests/` regress — run the full test project and confirm all pre-existing tests pass with the modified `LocalDispatchHelper`, `LocalChannelBackgroundService`, and `LocalTransportPublisher`

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — can start immediately
- **Foundational (Phase 2)**: Depends on Phase 1 — BLOCKS all user stories (T004–T005 must be complete)
- **US1 (Phase 3)**: Depends on Phase 2 — can start after T005
- **US2 (Phase 4)**: Depends on Phase 2 and US1 (needs `LocalConsumerBuilder` + working registration) — T012 modifies `LocalDispatchHelper` which is the core behavioral change
- **US3 (Phase 5)**: Depends on T012 (the `LocalDispatchHelper` signature change) — can proceed in parallel with US2 tests (T019–T021) once T012 is done
- **Polish (Phase 6)**: Depends on all user stories being complete

### User Story Dependencies

- **US1 (P1)**: Can start after Phase 2 — no dependencies on US2/US3
- **US2 (P2)**: Depends on US1 (T006 must be resolved first; T012 is the core change)
- **US3 (P3)**: Depends on T012 (same helper, same pattern) — can be done in parallel with US2 tests

### Within Each Phase

- T006 (internals visibility check) must complete before T007–T011 (tests need to compile)
- T004 must complete before T005 (builder depends on `LocalConsumerBuilder`)
- T012 must complete before T013 and T018 (both callers need the new signature)
- T013 must complete before T014–T017 (tests exercise the modified service)
- T018 must complete before T019–T021 (tests exercise the modified publisher)

### Parallel Opportunities

- T002 and T003 (Phase 1) run in parallel
- T007, T008, T009, T010, T011 (US1 tests) run in parallel after T006
- T014, T015, T016, T017 (US2 tests) run in parallel after T013
- T019, T020, T021 (US3 tests) run in parallel after T018
- T022 and T023 (Phase 6 doc tasks) run in parallel
- US2 tests (T014–T017) and US3 implementation (T018) can overlap after T012

---

## Parallel Example: User Story 1

```text
# After T006 resolves internals visibility, all US1 tests can run in parallel:
T007 — Subscribe registers handler
T008 — Subscribe is idempotent
T009 — Multiple handlers returned
T010 — Multiple AddLocalConsumer calls are additive
T011 — No handlers returns empty
```

## Parallel Example: User Story 2

```text
# After T013 (LocalChannelBackgroundService modified), all US2 tests run in parallel:
T014 — Registered handler is invoked
T015 — Unregistered handler not invoked
T016 — Fallback to DI scan works
T017 — NotHandled when no handlers
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup (T001–T003)
2. Complete Phase 2: Foundational (T004–T005) — **CRITICAL**
3. Complete Phase 3: US1 (T006–T011)
4. **STOP and VALIDATE**: `GetHandlersForEventAsync` returns registered types — registry works
5. This alone delivers SC-001 (introspection capability)

### Incremental Delivery

1. Setup + Foundational → `LocalConsumerBuilder` and `AddLocalConsumer` working
2. US1 → Handler registration queryable — demo-able as diagnostics/introspection tool
3. US2 → Local-channel dispatch uses registry — SC-002 achieved
4. US3 → Local (outbox) dispatch uses registry — SC-003 achieved, full parity
5. Polish → docs and regression check

### Parallel Team Strategy

With two developers after Phase 2:
- Dev A: US1 (T006–T011) → then US2 implementation (T012–T013)
- Dev B: Read ahead on US3 pattern → implement T018 once T012 is merged

---

## Notes

- `[P]` tasks = different files, no shared-state conflicts, safe to parallelize
- `[Story]` label maps each task to its user story for traceability
- Each user story is independently completable and testable
- No DB migrations needed — all changes are in-memory runtime only
- `IgnoreOnCIFact` is NOT needed for any of these tests — no external infra required
- `[InitializeMessageContext]` is required on test classes that exercise the local-channel dispatch path
- Commit after each checkpoint to keep the branch bisectable
