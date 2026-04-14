# Implementation Plan: Skip Already-Processed Local Deliveries in Phase 2

**Branch**: `010-local-delivery-skip` | **Date**: 2026-04-14 | **Spec**: [spec.md](spec.md)  
**Input**: Feature specification from `/specs/010-local-delivery-skip/spec.md`

## Summary

When a `"local"` route message is dispatched immediately in phase 1 and all handlers succeed, the outbox delivery record must be marked `Published` before the background delivery processor's next scan. This eliminates the redundant phase-2 cycle (pick up → mark InProgress → call transport → mark Published) for already-completed deliveries, and ensures phase 2 only handles genuine retries and failure recovery.

The implementation extends `ChannelEnvelope` with an optional post-dispatch callback. `MessageService<TContext>` builds that callback — capturing the `"local"` delivery IDs and `IOutboxRepository` factory — and attaches it to the envelope. `LocalChannelBackgroundService` invokes the callback after dispatch completes. If the callback fails (e.g., DB unavailable), the delivery remains `NotPublished` and phase 2 picks it up via its existing safety-net path in `LocalTransportPublisher`.

---

## Technical Context

**Language/Version**: C# on .NET 6 / .NET 8 / .NET 9  
**Primary Dependencies**: `Juice.Messaging.Local`, `Juice.Messaging`, `Juice.Messaging.Outbox`, `Juice.Messaging.Outbox.EF`  
**Storage**: EF Core (SQL Server + PostgreSQL) — no schema changes; `OutboxDelivery` record is written with `State = Published` earlier than before  
**Testing**: xUnit with `[InitializeMessageContext]`, `IgnoreOnCIFact` for EF/RabbitMQ integration tests  
**Target Platform**: Library (NuGet) — same multi-target as existing: `netstandard2.1`, runnable apps on `net6.0;net8.0;net9.0`  
**Project Type**: Library  
**Performance Goals**: Phase 2 performs zero transport calls for phase-1-completed deliveries (measured by `local_delivery_phase1_fallback_total` metric staying at 0 in normal operation)  
**Constraints**: No breaking changes to `ITransportPublisher`, `IOutboxService<TContext>`, or `ChannelEnvelope` public APIs used by consumers; `LocalChannelBackgroundService` must not depend on EF context type parameters  
**Scale/Scope**: Affects all `"local"` route deliveries; `"local-channel"`, `"rabbitmq"`, and other publisher keys are unaffected

---

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-checked post Phase 1 design.*

| Principle | Status | Notes |
|---|---|---|
| I. Lightweight & Dual-Architecture | PASS | No new abstractions without concrete use; callback pattern used immediately in `MessageService<TContext>` |
| II. Library-First Composability | PASS | Changes confined to `Juice.Messaging` and `Juice.Messaging.Local`; no new circular dependencies; no new library projects |
| III. DDD + CQRS | PASS | Infrastructure concern only; no domain model changes |
| IV. Reliable Messaging via Outbox Pattern | PASS | Outbox delivery state is now accurate after phase 1; at-least-once delivery preserved via callback-failure fallback |
| V. Multi-Tenancy First | PASS | No tenant isolation impact |

**Post-design re-check**: All principles pass. No violations.

---

## Project Structure

### Documentation (this feature)

```text
specs/010-local-delivery-skip/
├── plan.md         ← this file
├── research.md     ← Phase 0 output
├── data-model.md   ← Phase 1 output
└── tasks.md        ← Phase 2 output (/speckit.tasks — not created by /speckit.plan)
```

### Source Code (affected files)

```text
core/src/Juice.Messaging/
└── Internal/
    └── OutboxEventService.cs           ← expose GetPendingDeliveryIds(publisherKey)

core/src/Juice.Messaging.Local/
├── ChannelEnvelope.cs                  ← add OnDispatched callback
├── Internal/
│   ├── MessageServiceT.cs             ← build & attach callback for "local" routes
│   ├── LocalChannelBackgroundService.cs ← invoke callback post-dispatch
│   └── LocalTransportPublisher.cs     ← structured log for Duplicated fallback path

core/test/Juice.Integrations.Tests/
└── LocalDeliverySkipTest.cs           ← new integration test (IgnoreOnCIFact)
```

---

## Phase 0: Research

**Status**: Complete. See [research.md](research.md).

### Key Decisions

| Decision | Choice | Rationale |
|---|---|---|
| How phase 1 marks deliveries | Post-dispatch callback in `ChannelEnvelope` | Keeps `PublishAsync` non-blocking; avoids `LocalChannelBackgroundService` depending on EF context type |
| How delivery IDs reach the callback | `IOutboxService<TContext>.GetPendingDeliveryIds(publisherKey)` | In-memory snapshot already populated by `SaveEventsAsync`; no extra DB roundtrip |
| State for phase-1-completed deliveries | `Published` | Correct terminal state; `Skipped` is for unavailable/obsolete events |
| Fallback when callback fails | Leave `NotPublished`; phase 2 handles via existing `Duplicated` path | At-least-once delivery preserved; no silent message loss |
| `Duplicated` result handling in phase 2 | Structured warning log + `local_delivery_phase1_fallback_total` counter | Observability per FR-006; safety-net path distinguishable from normal delivery |

---

## Phase 1: Design & Contracts

**Status**: Complete. See [data-model.md](data-model.md).

### Design Summary

Four targeted changes across two libraries:

#### 1. `ChannelEnvelope` — add `OnDispatched` callback

```csharp
// Existing
public record ChannelEnvelope(IMessage Message, MessageContextSnapshot ContextSnapshot);

// New
public record ChannelEnvelope(
    IMessage Message,
    MessageContextSnapshot ContextSnapshot,
    Func<EventDispatchResult, CancellationToken, ValueTask>? OnDispatched = null);
```

`OnDispatched` is `null` for `"local-channel"` messages; non-null for `"local"` route messages.

#### 2. `IOutboxService<TContext>` — expose delivery IDs after save

```csharp
// New method on IOutboxService<TContext>
IReadOnlyList<Guid> GetPendingDeliveryIds(string publisherKey);
```

`OutboxEventService` already tracks created deliveries in-memory during `SaveEventsAsync`; this method returns that snapshot by publisher key. No DB roundtrip.

#### 3. `MessageService<TContext>` — build callback for `"local"` routes

After `SaveEventsAsync`, before enqueuing to channel:

```csharp
var localDeliveryIds = _outboxService.GetPendingDeliveryIds("local");
Func<EventDispatchResult, CancellationToken, ValueTask>? callback = null;

if (localDeliveryIds.Count > 0)
{
    callback = async (result, ct) =>
    {
        if (result is EventDispatchResult.Success or EventDispatchResult.Duplicated)
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var repo = scope.ServiceProvider.GetRequiredService<IOutboxRepository<TContext>>();
            foreach (var id in localDeliveryIds)
                await repo.MarkAsPublishedAsync(id, ct);
        }
    };
}

// Enqueue with callback
await _channelWriter.WriteAsync(
    new ChannelEnvelope(message, contextSnapshot, callback), ct);
```

#### 4. `LocalChannelBackgroundService` — invoke callback after dispatch

```csharp
// After DispatchIntegrationEventAsync or MediatR notification dispatch:
if (envelope.OnDispatched != null)
{
    try { await envelope.OnDispatched(result, cancellationToken); }
    catch (Exception ex)
    {
        _logger.LogWarning(ex,
            "Post-dispatch callback failed for message {MessageId}. " +
            "Delivery will be retried by background processor.",
            envelope.Message.MessageId);
    }
}
```

#### 5. `LocalTransportPublisher` — structured log for `Duplicated` fallback

```csharp
if (result == EventDispatchResult.Duplicated)
{
    _logger.LogWarning(
        "Event {EventName} (MessageId={MessageId}) was already processed in phase 1 " +
        "but delivery {PublisherKey} was not marked — completing via phase 2 fallback.",
        integrationEvent.EventName, integrationEvent.MessageId, Key);
    LocalChannelMetrics.IncrementPhase1Fallback(Key);
    // Do not throw — DeliveryProcessor marks Published normally.
}
```

### No External Contracts

This feature is entirely internal to the framework infrastructure. No public-facing extension method signatures change. No new NuGet packages. No EF migrations needed.

---

## Complexity Tracking

No constitution violations. No complexity justification required.

---

## Testing Strategy

### Unit tests (no infra)

- `ChannelEnvelope` with `OnDispatched` set: verify callback is invoked with correct `EventDispatchResult`.
- Callback with `Duplicated` result: verify `MarkAsPublishedAsync` is called.
- Callback with `Failure` result: verify `MarkAsPublishedAsync` is NOT called.
- Callback exception: verify `LocalChannelBackgroundService` catches it and continues processing next envelope.

### Integration tests (`IgnoreOnCIFact`, requires DB)

- **Happy path**: publish a `"local"` route message, verify handler invoked once, delivery record transitions directly to `Published` without ever entering `InProgress`.
- **Callback failure simulation**: break the DB after phase 1 dispatch but before callback completes; verify delivery stays `NotPublished`; restore DB; verify phase 2 picks up and marks `Published`.
- **Phase 2 fallback**: verify `local_delivery_phase1_fallback_total` counter increments when phase 2 encounters a `Duplicated` result (e.g., callback was skipped in test setup).
- **`"local-channel"` unaffected**: publish a `"local-channel"` route message; verify no callback is attached and no delivery record is written.
- **`"rabbitmq"` unaffected**: verify existing rabbitmq delivery behaviour is unchanged.

---

## Ready for `/speckit.tasks`

All research resolved, design complete, constitution check passed. The next step is `/speckit.tasks` to break the implementation into ordered, assignable tasks.
