# Data Model: Skip Already-Processed Local Deliveries in Phase 2

**Branch**: `010-local-delivery-skip`  
**Phase**: 1 — Design  
**Date**: 2026-04-14

---

## Existing Entities (unchanged)

### OutboxDelivery

No changes to the entity. The `Published` state already exists and is the correct terminal state for a phase-1-completed delivery. The `ProcessedOn` field is set to the timestamp of whichever phase completed the delivery.

```
OutboxDelivery
├── DeliveryId : Guid          (immutable, PK)
├── EventId    : Guid          (FK → OutboxEvent)
├── PublisherKey : string      (e.g. "local", "rabbitmq")
├── Destination  : string
├── RoutingKey   : string?
├── CreationTime : DateTimeOffset
├── State        : DeliveryState   ← published by phase 1 or phase 2
├── RetryCount   : int
├── ProcessedOn  : DateTimeOffset? ← set by whichever phase completes delivery
├── LastError    : string?
└── NextAttemptOn: DateTimeOffset?

DeliveryState:
  NotPublished = 0  (initial)
  InProgress   = 1  (claimed by phase 2)
  Published    = 2  (terminal — delivered by phase 1 OR phase 2)
  Failed       = 3  (phase 2 retry pending)
  Skipped      = 4  (destination unavailable / event obsolete — NOT used here)
```

---

## Modified: `ChannelEnvelope`

**Location**: `core/src/Juice.Messaging.Local/Internal/ChannelEnvelope.cs`

Add an optional list of outbox delivery IDs. When non-null and non-empty, `LocalChannelBackgroundService` knows there are outbox deliveries to mark `Published` after a successful dispatch. This is a pure data field — no behaviour/closure is embedded in the envelope.

```
ChannelEnvelope
├── Message          : IMessage                (existing)
├── Context          : MessageContextData?      (existing)
└── LocalDeliveryIds : IReadOnlyList<Guid>?    ← NEW (null for "local-channel"; delivery IDs for "local" route)
```

**Behaviour rules**:
- `LocalDeliveryIds` is `null` for `"local-channel"` messages (no outbox backing).
- `LocalDeliveryIds` is set for `"local"` route messages; populated from `IOutboxService.GetPendingDeliveryIds("local")` immediately after `SaveEventsAsync`.
- `LocalChannelBackgroundService` checks `LocalDeliveryIds` after dispatch completes. If non-null, it resolves `IOutboxRepository` (the non-generic base interface, which already has `MarkAsPublishedAsync`) from the current scope and calls it for each ID.


---

## Modified: `IOutboxService<TContext>` / `OutboxEventService`

**Location**: `core/src/Juice.Messaging.Outbox/` (interface) and `core/src/Juice.Messaging.Outbox.EF/` (implementation)

After `SaveEventsAsync`, the service must expose the delivery IDs that were created for the current batch, scoped by publisher key. This allows `MessageService<TContext>` to capture the delivery IDs for `"local"` route entries and pass them into the callback closure.

```
IOutboxService<TContext>
├── AddEventAsync(message, cancellationToken)    : ValueTask            (existing)
├── SaveEventsAsync(transactionId, ct)           : ValueTask            (existing)
└── GetPendingDeliveryIds(publisherKey)          : IReadOnlyList<Guid>  ← NEW
```

**Behaviour rules**:
- `GetPendingDeliveryIds(publisherKey)` returns the delivery IDs created in the most recent `SaveEventsAsync` call for the given `publisherKey`. Returns an empty list if `SaveEventsAsync` has not yet been called or no deliveries were created for that key.
- The list is cleared on the next `AddEventAsync` call (or reset per save cycle) — it is a transient, per-save snapshot, not a cumulative log.
- This method is synchronous and allocation-light (returns the IDs already tracked in-memory by `OutboxEventService`).

---

## Modified: `MessageService<TContext>` — local route publish path

**Location**: `core/src/Juice.Messaging/Internal/MessageServiceT.cs`

After `SaveEventsAsync`, for each message with a `"local"` route:

1. Call `_outboxService.GetPendingDeliveryIds("local")` to get the delivery IDs.
2. Pass those IDs as `LocalDeliveryIds` in the `ChannelEnvelope` when enqueuing to the channel.

No closure, no callback — just data.

**Behaviour rules**:
- If `GetPendingDeliveryIds("local")` returns empty, pass `null` (or omit the parameter); background service no-ops.

---

## Modified: `LocalChannelBackgroundService` — mark outbox state after dispatch

**Location**: `core/src/Juice.Messaging.Local/Internal/LocalChannelBackgroundService.cs`

After a **successful** dispatch (no exception thrown):

1. Check `envelope.LocalDeliveryIds is { Count: > 0 }`.
2. If true, resolve `ILocalOutboxDeliveryMarker` from the current scope (`sp.GetService<ILocalOutboxDeliveryMarker>()`).
3. If the marker is available, call `await marker.MarkPublishedAsync(envelope.LocalDeliveryIds, CancellationToken.None)`.
4. Wrap in try/catch; log warning on exception — delivery stays `NotPublished` for phase 2 retry.

**Behaviour rules**:
- `IOutboxRepository` is only resolved and called on success — exceptions in the dispatch path skip the block entirely (the `catch` exits before reaching it), leaving delivery `NotPublished`.
- `IOutboxRepository` resolves to `null` via `GetService` when no outbox repository is registered (graceful no-op for `"local-channel"`-only setups).
- The existing metric and error handling paths are unchanged.

---

## Modified: `LocalTransportPublisher` — structured log for phase-1 fallback

**Location**: `core/src/Juice.Messaging.Local/Internal/LocalTransportPublisher.cs`

When `IntegrationEventDispatcher.DispatchAsync` returns `EventDispatchResult.Duplicated` (meaning phase 1 already processed the event but the callback failed to mark it Published):

- Log a structured **warning**: `"Delivery {DeliveryId} for event {EventName} (MessageId={MessageId}) was already processed in phase 1 but its outbox record was not marked — completing now via phase 2."`
- Increment a dedicated metric counter: `local_delivery_phase1_fallback_total`.
- Do not throw — return normally so `DeliveryProcessor` marks it `Published` as usual.

**This path is the safety net**: it fires only when the phase 1 callback failed (e.g., DB connection loss between handler success and `MarkAsPublishedAsync`). In normal operation, the delivery record is `Published` before phase 2 scans, and `LocalTransportPublisher` is never called for it.

---

## State Transition Diagram

```
Phase 1 (happy path):
  NotPublished ──[SaveEventsAsync]──► NotPublished
                                          │
                              [LocalChannelBackgroundService dispatches]
                                          │
                              [OnDispatched callback: MarkAsPublishedAsync]
                                          ▼
                                       Published ◄── terminal; phase 2 never picks it up

Phase 1 (callback failure / crash):
  NotPublished ──► ... ──► NotPublished  (phase 2 picks it up)
                                          │
                         [DeliveryProcessor: MarkAsInProgressAsync]
                                          ▼
                                       InProgress
                                          │
                         [LocalTransportPublisher: Duplicated → log warning]
                                          │
                         [DeliveryProcessor: MarkAsPublishedAsync]
                                          ▼
                                       Published

Phase 1 (handler failure):
  NotPublished  (callback not invoked / invokes with Failure → no-op)
       │
  [DeliveryProcessor: normal retry path]
       ▼
  InProgress → Published (or Failed → retry)
```

---

## No New Libraries

All changes are within existing libraries. No new `core/src/` projects are required. No new EF migrations are needed (no schema changes — `OutboxDelivery` schema is unchanged; only the state value that gets written changes).
