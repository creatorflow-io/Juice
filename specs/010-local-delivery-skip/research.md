# Research: Skip Already-Processed Local Deliveries in Phase 2

**Branch**: `010-local-delivery-skip`  
**Phase**: 0 — Research  
**Date**: 2026-04-14

---

## Current Two-Phase Flow (as-built)

### Phase 1 — Immediate Dispatch

1. `MessageService<TContext>.PublishAsync` calls `SaveEventsAsync` → creates `OutboxDelivery` records in state `NotPublished`.
2. For `"local"` routes, calls `PublishLocalMessageAsync(message, true, ...)` → resolves `IMessagePublisher["local-channel"]` → enqueues `ChannelEnvelope` to the in-memory channel. This is fire-and-forget; the result is not captured.
3. `LocalChannelBackgroundService` drains the channel asynchronously → calls `LocalDispatchHelper.DispatchIntegrationEventAsync` → `IntegrationEventDispatcher.DispatchAsync`.
4. On success: idempotency key `"{source}:{messageId}"` scoped to `eventName` is recorded as `RequestState.Processed`.
5. The `OutboxDelivery` record **remains `NotPublished`**. Phase 1 has no access to delivery IDs and does not update them.

### Phase 2 — Background Delivery Processor

1. `DeliveryHostedService` (`SendPendingIntent`) scans for `State = NotPublished` records with `PublisherKey = "local"`.
2. `DeliveryProcessor.ProcessSingleDeliveryAsync`:
   - Calls `MarkAsInProgressAsync(deliveryId)` → `State = InProgress`, `ProcessedOn = now`.
   - Calls `LocalTransportPublisher.PublishAsync` → `IntegrationEventDispatcher.DispatchAsync`.
   - If phase 1 already succeeded: idempotency check returns `EventDispatchResult.Duplicated`. `LocalTransportPublisher` does not throw (only throws on `Failure`). Returns normally.
   - Calls `MarkAsPublishedAsync(deliveryId)` → `State = Published`, `ProcessedOn = now`.

### Current behaviour summary

When phase 1 succeeds, phase 2 still runs the full cycle: `NotPublished → InProgress → (Duplicated result, silently ignored) → Published`. Handlers are never invoked twice (idempotency). But the delivery processor still picks up the record, marks it `InProgress`, calls `LocalTransportPublisher`, and marks it `Published`.

---

## Key Findings

### Finding 1 — Phase 1 has no delivery IDs

`OutboxEventService.SaveEventsAsync` creates `OutboxDelivery` objects with generated IDs (`Guid.NewGuid()`), but does not return them. `MessageService<TContext>` only calls `SaveEventsAsync` and then enqueues the message to the channel. The delivery IDs are inaccessible from phase 1 code.

**Decision**: `OutboxEventService` (or the `IOutboxService<TContext>` interface) must expose the created delivery IDs for `"local"` route entries after `SaveEventsAsync`. This is the minimal structural change to enable phase 1 to mark deliveries.

**Alternatives considered**:
- Query the database for delivery IDs by `EventId` after save — rejected (extra DB roundtrip, race-prone).
- Carry delivery IDs in `ChannelEnvelope` only (no service interface change) — rejected because the IDs are not available until after `SaveEventsAsync`, which runs before the enqueue.

---

### Finding 2 — Channel dispatch is fire-and-forget; result unknown at enqueue time

`LocalChannelBackgroundService` dispatches asynchronously from the channel. At the moment `MessageService<TContext>.PublishAsync` returns, the handler has only been enqueued — not yet executed. This means the caller cannot synchronously know if phase 1 succeeded.

**Decision**: `LocalChannelBackgroundService` must invoke a post-dispatch callback after successful dispatch. The callback captures `MarkAsPublishedAsync` via closure registered by `MessageService<TContext>` at enqueue time. This keeps `PublishAsync` non-blocking and avoids `LocalChannelBackgroundService` taking a dependency on `IOutboxRepository<TContext>` (which requires the context type parameter it doesn't know).

**Alternatives considered**:
- Direct synchronous dispatch in `MessageService<TContext>` for `"local"` routes (await handlers inline, then mark Published) — viable but changes `PublishAsync` to block on handler completion, which may be surprising to callers and increases latency.
- `LocalChannelBackgroundService` takes `IOutboxRepository` and delivery IDs in the envelope — rejected because `LocalChannelBackgroundService` is a generic singleton that should not depend on EF context types.
- Check idempotency store in `DeliveryProcessor` before calling `LocalTransportPublisher` — rejected because it adds a cross-component coupling (delivery processor querying idempotency store), and still doesn't mark the record in a terminal state before the scan (violates FR-001/FR-005).

---

### Finding 3 — `EventDispatchResult.Duplicated` exists but is invisible to the delivery processor

`IntegrationEventDispatcher.DispatchAsync` returns `EventDispatchResult.Duplicated` when idempotency detects the event was already processed. `LocalTransportPublisher.PublishAsync` currently only throws on `Failure`; `Duplicated` is silently treated the same as `Success`. The delivery processor receives no signal about the duplication.

**Decision**: When phase 1 successfully marks the delivery as `Published` (via the callback mechanism), phase 2 will never pick up the record (it queries only `NotPublished`). The `Duplicated` path in `LocalTransportPublisher` becomes a fallback for edge cases (callback failed, crash between phase 1 dispatch and the mark). In that fallback, `LocalTransportPublisher` should log a structured warning: "Delivery {DeliveryId} was already processed by phase 1 immediate dispatch — marking Published".

---

### Finding 4 — `MarkAsInProgressAsync` is a concurrency guard

`OutboxRepository.MarkAsInProgressAsync` uses an `ExecuteUpdateAsync` with `WHERE State IN (NotPublished, Failed)` and returns affected row count. If 0, another worker already claimed it. This guard already prevents race conditions between multiple delivery processor instances. After our change, `Published` records are never fetched by the `SendPendingIntent` query (`WHERE State = NotPublished`), so the race scenario for phase-1-completed deliveries disappears entirely.

---

### Finding 5 — Delivery state for "already processed" case

When phase 1 marks the delivery `Published` and phase 2 never picks it up, `ProcessedOn` reflects phase 1's timestamp — which is earlier than the delivery processor's scan. This is correct: `ProcessedOn` means "when the delivery was completed", regardless of which phase completed it.

---

### Finding 6 — `DeliveryState.Skipped` exists

`OutboxDelivery` has a `Skipped` state (value 4) intended for "event is no longer relevant, destination unavailable." This is NOT the right state for phase-1-processed deliveries — those were successfully delivered and should be `Published`. Using `Skipped` would misrepresent the delivery outcome.

**Decision**: Use `Published` (not `Skipped`) when phase 1 completes the delivery. Reserve `Skipped` for its existing semantics (unavailable destination or obsolete event).

---

## Architecture Decision: Delivery Completion Callback

The design that satisfies all requirements without coupling `LocalChannelBackgroundService` to EF context types:

1. **`ChannelEnvelope`** — extended to carry an optional `Func<EventDispatchResult, CancellationToken, ValueTask>? OnDispatched` callback.
2. **`MessageService<TContext>`** — for `"local"` routes, creates the callback closure capturing `deliveryIds` and the scoped `IOutboxRepository<TContext>`. Passes it in the `ChannelEnvelope`.
3. **`LocalChannelBackgroundService`** — after `IntegrationEventDispatcher.DispatchAsync`, invokes `OnDispatched(result, ct)` if present. Does not interpret the result itself.
4. **`MessageService<TContext>` callback body**:
   - `Success` → `MarkAsPublishedAsync(deliveryId)` for each delivery ID
   - `Failure` / `NotHandled` → leave as `NotPublished` (phase 2 will retry or handle)
   - `Duplicated` → treated as success (idempotency says it was processed); call `MarkAsPublishedAsync`

This design:
- Keeps `LocalChannelBackgroundService` generic (no context type dependency)
- Keeps `PublishAsync` non-blocking (callback runs inside the background service's processing loop)
- Satisfies FR-001 (delivery marked terminal before phase 2's next scan)
- Satisfies FR-005 (skip decision in phase 2 is purely state-based)
- Does not affect `"local-channel"` route (no callback set → no-op)
- Does not affect non-`"local"` publisher keys in phase 2

---

## Libraries affected

| Library | Change |
|---|---|
| `Juice.Messaging.Local` | `ChannelEnvelope`: add optional `OnDispatched` callback; `LocalChannelBackgroundService`: invoke callback post-dispatch |
| `Juice.Messaging` | `IOutboxService<TContext>` (or `OutboxEventService`): expose delivery IDs for "local" routes after save |
| `Juice.Messaging.Local` | `MessageService<TContext>`: build and attach the callback for "local" routes |
| `Juice.Messaging.Local` | `LocalTransportPublisher`: add structured log when `Duplicated` (fallback path) |
| No new libraries required | All changes are within existing layer boundaries |

---

## Constitution Compliance

| Principle | Impact |
|---|---|
| I. Lightweight & Dual-Architecture | No new abstractions without concrete use; callback is used immediately |
| II. Library-First Composability | No new circular dependencies; changes stay within `Juice.Messaging` and `Juice.Messaging.Local` |
| III. DDD + CQRS | Not directly applicable (infrastructure concern) |
| IV. Reliable Messaging via Outbox Pattern | Strengthened — outbox delivery state is now accurate after phase 1 |
| V. Multi-Tenancy First | No impact |
