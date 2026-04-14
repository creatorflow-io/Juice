# Tasks: Skip Already-Processed Local Deliveries in Phase 2

**Input**: Design documents from `/specs/010-local-delivery-skip/`  
**Branch**: `010-local-delivery-skip`  
**Tests**: Integration tests included in Polish phase (not TDD; implementation first).

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies on incomplete tasks)
- **[Story]**: User story this task belongs to ([US1], [US2], [US3])

---

## Phase 1: Setup

No new projects or packages required. All changes are within existing libraries. Skip directly to Foundational.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Interface and data model changes that all story tasks depend on. Must complete before Phase 3.

**⚠️ CRITICAL**: All user story tasks depend on T001–T003.

- [X] T001 Extend `ChannelEnvelope` record with an optional `IReadOnlyList<Guid>? LocalDeliveryIds = null` parameter (null for "local-channel" messages, populated with outbox delivery IDs for "local" route messages) in `core/src/Juice.Messaging.Local/Internal/ChannelEnvelope.cs`

- [X] T002 Add `IReadOnlyList<Guid> GetPendingDeliveryIds(string publisherKey)` method to `IOutboxService` interface in `core/src/Juice.Messaging/Outbox/IOutboxService.cs`

- [X] T003 Implement `GetPendingDeliveryIds` in `OutboxEventService`: track created `OutboxDelivery` IDs per publisher key during `SaveEventsAsync` in an internal `Dictionary<string, List<Guid>>` field (cleared at the start of each `SaveEventsAsync`), and return the matching list in `GetPendingDeliveryIds` in `core/src/Juice.Messaging.Outbox/Internal/OutboxEventService.cs`

**Checkpoint**: `ChannelEnvelope` carries delivery IDs as data; `IOutboxRepository` (non-generic, already has `MarkAsPublishedAsync`) is resolvable directly. Story tasks can now begin.

---

## Phase 3: User Story 1 — Phase 1 Success Closes Delivery Record (Priority: P1) 🎯 MVP

**Goal**: When all handlers succeed during phase 1 immediate dispatch, the outbox delivery record is marked `Published` — the background delivery processor never picks it up.

**Independent Test**: Publish a `"local"` route message with a succeeding handler. Verify the `OutboxDelivery` record transitions directly from `NotPublished` to `Published` without ever entering `InProgress`. Confirm the background delivery processor emits no InProgress log for that delivery.

- [X] T007 [US1] In `MessageService<TContext>` (in the code path that calls `SaveEventsAsync` then enqueues for `"local"` routes): after `SaveEventsAsync`, call `_outboxService.GetPendingDeliveryIds("local")`; pass the result as `LocalDeliveryIds` in the `ChannelEnvelope` constructor when calling `PublishLocalMessageAsync` (or equivalent). If the list is empty, pass `null`. No closure, no callback — data only. In `core/src/Juice.Messaging/Internal/MessageServiceT.cs`

- [X] T008 [US1] In `LocalChannelBackgroundService`, after the dispatch succeeds (inside the `try` block, after both `IIntegrationEvent` and `INotification` dispatch paths, before the metrics calls): check `envelope.LocalDeliveryIds is { Count: > 0 }`; if true, resolve `sp.GetService<IOutboxRepository>()`; if non-null, call `await repo.MarkAsPublishedAsync(id, CancellationToken.None)` for each ID inside a nested try/catch that logs `LogWarning` "Failed to mark local delivery Published for message {MessageId} — delivery will be retried by background processor" on exception. In `core/src/Juice.Messaging.Local/Internal/LocalChannelBackgroundService.cs`

**Checkpoint**: Publish a `"local"` message → handler runs → delivery goes directly to `Published`. Background processor sees no `NotPublished` records. User Story 1 independently testable.

---

## Phase 4: User Story 2 — Phase 1 Failure Falls Back to Phase 2 (Priority: P1)

**Goal**: When phase 1 dispatch fails (handler throws, `NotHandled`, or callback exception), the outbox delivery record stays `NotPublished` and the background delivery processor picks it up and retries normally.

**Independent Test**: Register a throwing handler for a `"local"` route message. Publish the message. Verify the delivery stays `NotPublished`. Confirm the delivery processor picks it up, marks `InProgress`, calls the handler again, and marks `Published` (or `Failed` if the handler still throws).

- [X] T009 [US2] Add a code comment above the `MarkPublishedAsync` call inside `LocalChannelBackgroundService` (T008) explaining the failure invariant: "If the marker throws (e.g. DB unavailable), the exception is caught and logged here. The delivery record remains NotPublished — the background delivery processor will pick it up and retry. No message is lost." in `core/src/Juice.Messaging.Local/Internal/LocalChannelBackgroundService.cs`

- [X] T010 [US2] Add a code comment above `ProcessSingleDeliveryAsync` in `DeliveryProcessor` explaining the phase-1 relationship: "For 'local' publisher: if phase 1 succeeded and LocalChannelBackgroundService marked the record Published, SendPendingIntent never retrieves it (filters to NotPublished only). This path handles phase 1 failures and crash-recovery scenarios." in `core/src/Juice.Messaging.Outbox.Delivery/Processing/DeliveryProcessor.cs`

**Checkpoint**: Force a handler failure → delivery stays `NotPublished` → delivery processor picks it up. User Story 2 independently testable.

---

## Phase 5: User Story 3 — Observability: Phase-1 Fallback Distinguishable (Priority: P2)

**Goal**: When the background delivery processor encounters a `"local"` delivery that was already processed by phase 1 (idempotency returns `Duplicated`), it emits a structured warning log and increments a dedicated metric counter — distinguishable from normal delivery completions.

**Independent Test**: Disable the `OnDispatched` callback in a test (or simulate a callback failure). Publish a `"local"` message, let phase 1 dispatch run without marking Published. Let phase 2 process the delivery. Confirm a `LogWarning` entry with "already processed in phase 1" is present and that `local_delivery_phase1_fallback_total` counter increments.

- [X] T011 [US3] Add a `static void IncrementPhase1Fallback(string publisherKey)` method to `LocalChannelMetrics` that increments a counter named `"local_delivery_phase1_fallback_total"` tagged with `publisher = publisherKey`, following the same pattern as existing `IncrementDeliverySuccess` in `core/src/Juice.Messaging.Local/Internal/LocalChannelMetrics.cs`

- [X] T012 [P] [US3] In `LocalTransportPublisher.PublishAsync`, after `LocalDispatchHelper.DispatchIntegrationEventAsync` returns `EventDispatchResult.Duplicated`: add `_logger.LogWarning("Event {EventName} (MessageId={MessageId}) was already processed in phase 1 immediate dispatch but its delivery record was not marked Published — completing via phase 2 fallback. PublisherKey={PublisherKey}.", integrationEvent.EventName, integrationEvent.MessageId, Key)` and call `LocalChannelMetrics.IncrementPhase1Fallback(Key)`. Do not throw — return normally so `DeliveryProcessor` marks the record `Published`. In `core/src/Juice.Messaging.Local/Internal/LocalTransportPublisher.cs`

**Checkpoint**: Force phase-2 fallback path → warning log appears → counter increments. User Story 3 independently testable.

---

## Phase 6: Polish & Cross-Cutting Concerns

- [X] T013 [P] Add XML doc comment to `ChannelEnvelope.LocalDeliveryIds` explaining: "Outbox delivery IDs for `'local'` route messages. When non-null, `LocalChannelBackgroundService` calls `ILocalOutboxDeliveryMarker.MarkPublishedAsync` after a successful dispatch. Null for `'local-channel'` messages (no outbox backing)." in `core/src/Juice.Messaging.Local/Internal/ChannelEnvelope.cs`

- [X] T014 [P] Add XML doc comment to `IOutboxService.GetPendingDeliveryIds` explaining: "Returns the delivery IDs created for the given `publisherKey` during the most recent `SaveEventsAsync` call. Returns an empty list if no deliveries were created for that key. This snapshot is cleared at the start of each new `SaveEventsAsync` cycle." in `core/src/Juice.Messaging/Outbox/IOutboxService.cs`

- [X] T015 Integration test — US1 happy path: publish a `"local"` route `IIntegrationEvent` with a succeeding handler, then assert: (a) handler invoked exactly once, (b) `OutboxDelivery.State == Published` without any background delivery processor cycle. Use `IgnoreOnCIFact` and `[InitializeMessageContext]`. Add to `core/test/Juice.Messaging.Local.Tests/LocalDeliverySkipTests.cs`

- [X] T016 [P] Integration test — US2 failure fallback: register a throwing handler for a `"local"` route message, publish it, assert `State == NotPublished` after publish. Let the delivery processor run and assert delivery eventually reaches `Published` or `Failed`. Use `IgnoreOnCIFact`. Add to `core/test/Juice.Messaging.Local.Tests/LocalDeliverySkipTests.cs`

---

## Dependencies & Execution Order

### Phase Dependencies

- **Foundational (Phase 2)**: No dependencies — start immediately. **BLOCKS all story phases.**
  - T001–T003 can be done in parallel (different files).
- **US1 (Phase 3)**: Requires T001–T003 complete. T007 and T008 can be done in parallel (different files).
- **US2 (Phase 4)**: Requires T007–T008 complete. T009–T010 are comment-only, parallel.
- **US3 (Phase 5)**: Independent of US1/US2. T011 and T012 can be done in parallel.
- **Polish (Phase 6)**: T013–T014 independent of each other. T015–T016 require all story phases complete.

### User Story Dependencies

- **US1 (P1)**: After Foundational — no dependency on US2 or US3.
- **US2 (P1)**: After US1 — verifies failure invariants in files already modified.
- **US3 (P2)**: After Foundational (T001) — independent of US1/US2.

### Parallel Opportunities

- T004 and T005 (Foundational) — different files, parallel.
- T007 and T008 (US1) — different files, parallel.
- T009 and T010 (US2) — different files, parallel.
- T011 and T012 (US3) — different files, parallel.
- T013 and T014 (Polish docs) — different files, parallel.
- T015 and T016 (Polish tests) — same test file; sequential or separate test classes.

---

## Parallel Example: Foundational → US1

```
Parallel: T001 (ChannelEnvelope — add LocalDeliveryIds)
          T002 (IOutboxService interface — add GetPendingDeliveryIds)
          T003 (OutboxEventService impl — track and expose delivery IDs)

After T001 + T002 + T003:
  Parallel: T007 (MessageServiceT.cs — populate LocalDeliveryIds in envelope)
            T008 (LocalChannelBackgroundService — resolve IOutboxRepository and call MarkAsPublishedAsync)

US3 (independent — needs no Foundational tasks):
  Parallel: T011 (LocalChannelMetrics — add phase1 fallback counter)
            T012 (LocalTransportPublisher — log + counter on Duplicated)
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 2: Foundational (T001–T003)
2. Complete Phase 3: US1 (T007–T008)
3. **STOP and VALIDATE**: Confirm `"local"` deliveries go directly to `Published` without delivery processor involvement.
4. Polish T013–T014 (docs) — optional before shipping MVP.

### Incremental Delivery

1. **Foundational** → foundation ready (T001–T003)
2. **US1** → core happy path (T004–T005) → delivery processor no longer re-processes phase-1-complete records
3. **US2** → failure path verified/documented (T006–T007) → at-least-once delivery confirmed correct
4. **US3** → observability (T008–T009) → operators can distinguish fallback from normal delivery
5. **Polish** → tests + docs (T010–T013)

---

## Notes

- **No breaking changes**: `ChannelEnvelope` gains an optional parameter (default `null`) — existing call sites unaffected. No closures or callbacks in the envelope — data only.
- **No schema changes**: `OutboxDelivery` schema unchanged; only which phase writes `Published` changes.
- **`"local-channel"` unaffected**: No `OnDispatched` callback is set → background service no-ops on the null check.
- **`"rabbitmq"` unaffected**: Delivery processor path for non-`"local"` keys is unchanged.
- **Crash safety**: If process crashes after phase 1 dispatch but before callback fires, delivery stays `NotPublished`. Phase 2 picks it up. `IntegrationEventDispatcher` idempotency prevents double invocation → `Duplicated` result → US3 fallback log fires → delivery marked `Published`. No message lost.
