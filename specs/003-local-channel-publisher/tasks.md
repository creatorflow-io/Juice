# Tasks: Local Transport Publishers with Unified Message Service

**Input**: Design documents from `specs/003-local-channel-publisher/`
**Branch**: `003-local-channel-publisher`
**Stack**: C# / .NET 6, 8, 9 — NuGet library `core/src/Juice.Messaging.Local`

## Format: `[ID] [P?] [Story?] Description`

- **[P]**: Can run in parallel (different files, no incomplete dependencies)
- **[Story]**: User story this task belongs to (US1–US4)

---

## Phase 1: Setup (Project Initialization)

**Purpose**: Create new library and test project skeletons before any story work begins.

- [X] T001 Create `core/src/Juice.Messaging.Local/Juice.Messaging.Local.csproj` targeting `net6.0;net8.0;net9.0` with refs to `Juice.Messaging`, `Juice.MediatR`, `Juice.EventBus`, `Juice.Messaging.Outbox`
- [X] T002 Create `core/test/Juice.Messaging.Local.Tests/Juice.Messaging.Local.Tests.csproj` with refs to `Juice.Messaging.Local` and xUnit / test infrastructure
- [X] T003 [P] Create `core/src/Juice.Messaging.Local/Assembly.cs` (assembly marker, mirrors pattern from existing projects)

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Interfaces and shared dispatch logic that every user story implementation depends on.

**⚠️ CRITICAL**: No user story work can begin until this phase is complete.

- [X] T004 Add `IMessageService` interface (non-generic) to `core/src/Juice.Messaging/IMessageService.cs` — single method `Task PublishAsync(IMessage message, CancellationToken cancellationToken = default)`
- [X] T005 [P] Add `IMessageService<TContext>` interface to `core/src/Juice.Messaging/IMessageService.cs` — extends `IMessageService`, `where TContext : class`, no additional members
- [X] T006 [P] Add `LocalChannelOptions` class to `core/src/Juice.Messaging.Local/LocalChannelOptions.cs` — single property `int? MaxConcurrency` (null = unlimited)
- [X] T007 Add `LocalDispatchHelper` to `core/src/Juice.Messaging.Local/Internal/LocalDispatchHelper.cs` — shared static class with `ConcurrentDictionary<Type, MethodInfo>` cache; methods: `DispatchNotificationAsync(INotificationPublisher, IMessage, CancellationToken)` (reflected `Publish<T>`) and `DispatchIntegrationEventAsync(IServiceProvider, IntegrationEventDispatcher, IIntegrationEvent, CancellationToken)` (DI-resolved handlers → `EventDispatchContext` → `DispatchAsync`)

**Checkpoint**: Interfaces defined, shared dispatch logic available — user story implementation can begin.

---

## Phase 3: User Story 1 — Publish In-Process Without Database (Priority: P1) 🎯 MVP

**Goal**: `IMessageService.PublishAsync` with `"local-channel"` policy enqueues to in-memory channel; `LocalChannelBackgroundService` dispatches handlers concurrently; zero DB writes.

**Independent Test**: Configure `"local-channel"` policy rule, call `PublishAsync`, assert no `OutboxDeliveries` rows written, assert handler side-effect is present after `Task.Delay` (async settle).

- [X] T008 [US1] Implement `LocalChannelBackgroundService` in `core/src/Juice.Messaging.Local/Internal/LocalChannelBackgroundService.cs` — `BackgroundService`; constructor takes `ChannelReader<IMessage>`, `IOptions<LocalChannelOptions>`, `IServiceScopeFactory`, `ILogger`; single reader loop with `SemaphoreSlim(MaxConcurrency)` guard; spawns `Task.Run(..., CancellationToken.None)` per message; each task creates new DI scope, calls `LocalDispatchHelper`; catches all exceptions, logs, emits `DeliveryMetrics` for key `"local-channel"`
- [X] T009 [P] [US1] Implement non-generic `MessageService` in `core/src/Juice.Messaging.Local/Internal/MessageService.cs` — scoped; constructor takes `IMessagePublishingPolicy`, `ChannelWriter<IMessage>`, `ITenantAccessor?`; `PublishAsync` resolves routes, enqueues to channel for `"local-channel"` routes only, ignores other routes (caller must use `IMessageService<TContext>` for those)
- [X] T010 [US1] Add `AddLocalChannel(Action<LocalChannelOptions>? configure = null)` extension on `MessagingBuilder` in `core/src/Juice.Messaging.Local/DependencyInjection/LocalMessagingBuilderExtensions.cs` — registers `Channel<IMessage>` (singleton, unbounded, `SingleReader=true`, `AllowSynchronousContinuations=false`), `LocalChannelOptions` via `Configure`, `LocalChannelBackgroundService` as hosted service, `MessageService` as `IMessageService` (scoped); guards with `TryAdd` to prevent double-registration
- [X] T011 [US1] Add integration test `LocalChannelTests` to `core/test/Juice.Messaging.Local.Tests/LocalChannelTests.cs` — verify: (a) `PublishAsync` returns before handler runs; (b) handler eventually invoked; (c) zero rows in `OutboxDeliveries`; (d) `MaxConcurrency` limits simultaneous handler executions; use `[InitializeMessageContext]` and `IgnoreOnCIFact`

**Checkpoint**: US1 fully functional — zero-DB local-channel dispatch works end-to-end, concurrent dispatch verified.

---

## Phase 4: User Story 2 — Durable In-Process Delivery via Local Route (Priority: P1)

**Goal**: `IMessageService<TContext>.PublishAsync` with `"local"` policy writes an outbox row; existing `DeliveryHostedService` picks it up; `LocalTransportPublisher` dispatches to in-process handler; retry and recovery inherited from outbox infrastructure.

**Independent Test**: Configure `"local"` policy rule, call `PublishAsync` inside a TX scope, assert one `OutboxDelivery` row with `PublisherKey = "local"`, assert handler is invoked after delivery service runs, assert failed delivery increments `RetryCount`.

- [X] T012 [US2] Implement `LocalTransportPublisher` in `core/src/Juice.Messaging.Local/Internal/LocalTransportPublisher.cs` — scoped; implements `ITransportPublisher` with `Key = "local"`; constructor takes `IMessageSerializer`, `IServiceScopeFactory`, `IntegrationEventDispatcher`, `INotificationPublisher`, `ILogger`; `PublishAsync(byte[], PublishContext, ct)`: extract type name from `context.Headers["x-message-type"]`, deserialize, branch via `LocalDispatchHelper`; emit `DeliveryMetrics` with key `"local"`
- [X] T013 [P] [US2] Implement `MessageService<TContext>` in `core/src/Juice.Messaging.Local/Internal/MessageServiceT.cs` — scoped; extends `MessageService`; constructor additionally takes `IOutboxService<TContext>`; `PublishAsync` override: handles `"local-channel"` routes via base; for remaining routes calls `_outboxService.AddEventAsync(msg)`; then calls `_outboxService.SaveEventsAsync(null, ct)` immediately (OutboxRepository joins ambient transaction when present)
- [X] T014 [US2] Extend `LocalMessagingBuilderExtensions.cs` in `core/src/Juice.Messaging.Local/DependencyInjection/LocalMessagingBuilderExtensions.cs` — add `AddLocalPublisher<TContext>()`: registers `LocalTransportPublisher` as keyed `ITransportPublisher` `"local"` (scoped); add `AddMessageService<TContext>()`: calls `AddLocalChannel()` if not already registered, registers `MessageService<TContext>` as `IMessageService<TContext>` (scoped)
- [X] T015 [US2] Add integration test `LocalTransportPublisherTests` to `core/test/Juice.Messaging.Local.Tests/LocalTransportPublisherTests.cs` — verify: (a) handler called in-process; (b) failed handler propagates exception for retry scheduling; use `[InitializeMessageContext]` and `IgnoreOnCIFact`

**Checkpoint**: US2 fully functional — durable local dispatch writes to outbox and delivers in-process with retry.

---

## Phase 5: User Story 3 — Unified Publish API for All Route Types (Priority: P2)

**Goal**: Single `IMessageService.PublishAsync` call transparently routes to `"local-channel"`, `"local"`, or broker — no application code change when config changes. Multi-route policies deliver to all resolved routes independently.

**Independent Test**: Publish same event under three config variants (local-channel / local / rabbitmq); verify correct transport used each time with zero code change. Publish with multi-route policy; verify both routes deliver independently.

- [X] T016 [P] [US3] Add integration test scenario to `core/test/Juice.Messaging.Local.Tests/MessageServiceTests.cs` — verify domain event (`INotification`) dispatches via `INotificationPublisher.Publish<T>()` on `"local-channel"` route (not via `IIntegrationEventHandler`)
- [X] T017 [P] [US3] Add integration test scenario to `core/test/Juice.Messaging.Local.Tests/MessageServiceTests.cs` — verify routing config swap (same `PublishAsync` call, different policy config) changes transport without application code modification
- [X] T018 [US3] Add integration test scenario to `core/test/Juice.Messaging.Local.Tests/MessageServiceTests.cs` — verify `IMessageService<TContext>` injection point unchanged across all three route types

**Checkpoint**: US3 verified — unified API confirmed config-driven across all transport modes.

---

## Phase 6: User Story 4 — Handler Failure Isolation (Priority: P3)

**Goal**: Handler exceptions in both local delivery paths are contained — background service does not crash; caller is never affected; subsequent messages are processed normally.

**Independent Test**: Register a handler that throws; publish via local-channel and via "local" route; verify background service continues processing subsequent messages; verify "local" delivery is marked failed and retry is scheduled.

- [X] T019 [P] [US4] Add test scenario to `core/test/Juice.Messaging.Local.Tests/LocalChannelTests.cs` — `LocalChannelBackgroundService` exception isolation: throwing handler → error logged → service continues → subsequent message handled; verify `DeliveryMetrics` failure counter incremented for `"local-channel"`
- [X] T020 [P] [US4] Add test scenario to `core/test/Juice.Messaging.Local.Tests/LocalTransportPublisherTests.cs` — `LocalTransportPublisher` exception isolation: throwing handler → exception propagates → `DeliveryProcessor` marks delivery `Failed` → `RetryCount` incremented → `NextAttemptOn` set → other pending deliveries unaffected

**Checkpoint**: US4 verified — all failure isolation scenarios confirmed for both local delivery paths.

---

## Phase 7: Immediate Dispatch & Idempotency for "local" Route

**Goal**: After outbox commit, `"local"` route messages are immediately enqueued to the in-memory channel for near-zero-latency dispatch. Idempotency ensures the handler runs exactly once even when both immediate dispatch and `DeliveryHostedService` retry fire for the same message. `LocalTransportPublisher` restores `MessageContext` from outbox headers for consistent idempotency keys. `IntegrationEventDispatcher` supports handler resolution by interface type (fallback from concrete type).

- [X] T025 [US5] Update `IntegrationEventDispatcher` in `core/src/Juice.Messaging/Integrations/IntegrationEventDispatcher.cs` — when concrete handler type resolution fails, fall back to resolving `IIntegrationEventHandler<T>` from DI and matching by concrete type; supports both RabbitMQ (concrete type registration) and local-channel (interface registration) patterns
- [X] T026 [US5] Update `LocalTransportPublisher` in `core/src/Juice.Messaging.Local/Internal/LocalTransportPublisher.cs` — initialize `MessageContext` from outbox headers (`x-source`, `x-correlation-id`, `x-causation-id`) when not already initialized; clear in `finally` block; check dispatch result and throw on `Failure` so `DeliveryProcessor` schedules retry
- [X] T027 [US5] Update `LocalDispatchHelper` in `core/src/Juice.Messaging.Local/Internal/LocalDispatchHelper.cs` — change `DispatchIntegrationEventAsync` return type from `Task` to `Task<EventDispatchResult>` so callers can react to dispatch failures
- [X] T028 [US5] Update `MessageService<TContext>` in `core/src/Juice.Messaging.Local/Internal/MessageServiceT.cs` — after outbox save, if route has `PublisherKey == "local"` and no `"local-channel"` route already enqueued, enqueue message to in-memory channel for immediate best-effort dispatch
- [X] T029 [US5] Add `IdempotencyDeduplicationTests` to `core/test/Juice.Messaging.Local.Tests/IdempotencyDeduplicationTests.cs` — tests: (a) same message dispatched via channel then via `LocalTransportPublisher` → handler runs once (idempotency dedup); (b) different `MessageId`s → handler runs twice (no dedup); (c) `LocalTransportPublisher` restores `MessageContext` from headers and deduplicates on second call; uses singleton `IIdempotencyService` for cross-scope dedup

**Checkpoint**: US5 verified — immediate dispatch with idempotency deduplication works end-to-end; `LocalTransportPublisher` correctly restores `MessageContext` for consistent idempotency keys.

---

## Phase 8: Polish & Cross-Cutting Concerns

- [X] T021 [P] Add XML doc comments to `IMessageService` in `core/src/Juice.Messaging/IMessageService.cs` — document non-generic scope, local-channel-only behavior, and pointer to `IMessageService<TContext>`
- [X] T022 [P] Add XML doc comments to `IMessageService<TContext>` in `core/src/Juice.Messaging/IMessageService.cs` — document transaction-detection behavior, all three route types, and required DI registration
- [X] T023 [P] Add XML doc comments to `LocalMessagingBuilderExtensions` in `core/src/Juice.Messaging.Local/DependencyInjection/LocalMessagingBuilderExtensions.cs` — document `AddLocalChannel`, `AddLocalPublisher<TContext>`, `AddMessageService<TContext>` with example configuration
- [X] T024 Verify `DeliveryMetrics` emits correctly for both `"local"` and `"local-channel"` publisher keys — `LocalChannelMetrics` (internal static class in `Juice.Messaging.Local`) uses meter name `"delivery.metrics"` with same counter/histogram names; `LocalTransportPublisher` emits for `"local"` key; `LocalChannelBackgroundService` emits for `"local-channel"` key

---

## Dependencies & Execution Order

### Phase Dependencies

- **Phase 1 (Setup)**: No dependencies — start immediately
- **Phase 2 (Foundational)**: Depends on Phase 1 — **BLOCKS all user stories**
- **Phase 3 (US1)**: Depends on Phase 2
- **Phase 4 (US2)**: Depends on Phase 2; T012/T013 also depend on T007 (`LocalDispatchHelper`)
- **Phase 5 (US3)**: Depends on Phase 3 + Phase 4 (verifies unified behavior of both)
- **Phase 6 (US4)**: Depends on Phase 3 + Phase 4 (tests failure paths in both services)
- **Phase 7 (US5 — Idempotency)**: Depends on Phase 3 + Phase 4 (modifies dispatcher, publisher, and message service)
- **Phase 8 (Polish)**: Depends on all story phases complete

### User Story Dependencies

- **US1 (P1)**: Depends on Foundational only — independent of US2
- **US2 (P1)**: Depends on Foundational + T007 (`LocalDispatchHelper`) — independent of US1; can start in parallel with US1 after T007 completes
- **US3 (P2)**: Depends on US1 + US2 complete (verifies integration of both)
- **US4 (P3)**: Depends on US1 + US2 complete (tests failure paths of both)
- **US5 (P2)**: Depends on US1 + US2 complete (immediate dispatch + idempotency across dispatch paths)

### Within Each Story

- `LocalDispatchHelper` (T007) before `LocalChannelBackgroundService` (T008) and `LocalTransportPublisher` (T012)
- Background service (T008/T012) before DI registration (T010/T014)
- DI registration before integration tests (T011/T015)
- Interfaces (T004/T005) before implementations (T009/T013)

---

## Parallel Opportunities

### Phase 2 — run together

```
T004 IMessageService interface
T005 IMessageService<TContext> interface      ← parallel with T004
T006 LocalChannelOptions                      ← parallel with T004, T005
```
Then T007 (LocalDispatchHelper) after T004/T005.

### Phase 3 + Phase 4 — US1 and US2 after T007

```
Developer A: T008 → T009 → T010 → T011   (US1 local-channel path)
Developer B: T012 → T013 → T014 → T015   (US2 local route path)
```

### Phase 7 — all polish tasks parallel

```
T021, T022, T023, T024  ← all different files, no dependencies between them
```

---

## Implementation Strategy

### MVP (US1 only — local-channel, zero DB)

1. Complete Phase 1: Setup
2. Complete Phase 2: T004 → T005 → T006 → T007
3. Complete Phase 3: T008 → T009 → T010 → T011
4. **STOP and VALIDATE**: zero-DB local-channel dispatch end-to-end
5. Register `AddLocalChannel()` in a host project, verify handler fires

### Incremental Delivery

1. Phase 1 + 2 → foundation ready
2. Phase 3 (US1) → MVP: `"local-channel"` works ✅
3. Phase 4 (US2) → `"local"` durable dispatch works ✅
4. Phase 5 (US3) → unified API verified across all routes ✅
5. Phase 6 (US4) → failure isolation confirmed ✅
6. Phase 7 (US5) → immediate dispatch + idempotency deduplication ✅
7. Phase 8 → polish and docs

---

## Notes

- `[P]` tasks touch different files — safe to parallelize
- `[US*]` labels map directly to user stories in `spec.md`
- Tests use `[InitializeMessageContext]` + `IgnoreOnCIFact` per framework convention
- No new EF migrations — all DB interaction through existing outbox tables
- `LocalDispatchHelper` (T007) is the only shared dependency between US1 and US2 implementation tasks
- `OutboxEventService.SaveEventsAsync` was updated to skip `"local-channel"` routes (prevents spurious OutboxDelivery entries, satisfies FR-004)
- `LocalChannelMetrics` (internal, in `Juice.Messaging.Local`) mirrors `DeliveryMetrics` using same meter name `"delivery.metrics"` for unified observability
