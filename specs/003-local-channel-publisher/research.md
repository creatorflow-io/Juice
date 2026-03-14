# Research: Local Transport Publishers with Unified Message Service

**Date**: 2026-03-14 | **Branch**: `003-local-channel-publisher`

---

## R-001: Runtime dispatch of `INotification` via reflected `INotificationPublisher.Publish<T>`

**Decision**: Use reflection with a `ConcurrentDictionary<Type, MethodInfo>` cache — identical pattern to `IntegrationEventDispatcher._methodCache`.

**Rationale**: `INotificationPublisher.Publish<T>(T notification, CancellationToken)` is generic. At runtime we have an `IMessage` instance whose concrete type is only known at runtime. The MediatR mediator resolves `INotificationHandler<T>` by the concrete `T`, so we must call `Publish<ConcreteType>`, not `Publish<INotification>`. Reflection with a warm cache has negligible overhead (single dictionary lookup after first call per type).

**Implementation sketch**:
```
private static readonly ConcurrentDictionary<Type, MethodInfo> _publishMethodCache = new();

var method = _publishMethodCache.GetOrAdd(msg.GetType(), t =>
    typeof(INotificationPublisher)
        .GetMethod(nameof(INotificationPublisher.Publish))!
        .MakeGenericMethod(t));

await (ValueTask)method.Invoke(_publisher, [msg, ct])!;
```

**Alternatives considered**:
- `_mediator.Publish<INotification>(notification)` — rejected: dispatches to handlers of `INotification` (the base interface), not the concrete type's handlers.
- Source-generated dispatch — rejected: overkill; framework already uses the reflection-cache pattern in `IntegrationEventDispatcher`.

---

## R-002: `IIntegrationEvent` handler auto-discovery from DI

**Decision**: Resolve `IEnumerable<IIntegrationEventHandler<T>>` from the DI scope via `GetServices(typeof(IIntegrationEventHandler<>).MakeGenericType(concreteType))`, collect handler types, build `EventDispatchContext`, pass to `IntegrationEventDispatcher.DispatchAsync`.

**Rationale**: No `ISubscriptionsManager` registration needed. All registered `IIntegrationEventHandler<T>` implementations in the DI container are automatically discovered. This matches the Q1 clarification (Option B: auto-discover from DI).

**Implementation sketch**:
```
var handlerType = typeof(IIntegrationEventHandler<>).MakeGenericType(evt.GetType());
var handlerTypes = scope.ServiceProvider
    .GetServices(handlerType)
    .Select(h => h!.GetType())
    .ToList();
var ctx = new EventDispatchContext(handlerTypes, evt.GetType().Name, tenantId, source);
await _dispatcher.DispatchAsync(evt, ctx);
```

**Alternatives considered**:
- Reuse `ISubscriptionsManager` — rejected: requires explicit subscription registration for local routes; adds friction with no benefit (broker routing key mapping is irrelevant for local dispatch).
- Separate registration API — rejected: violates the no-extra-registration requirement from Q1.

---

## R-003: `Channel<IMessage>` patterns for `"local-channel"` background service

**Decision**: `Channel.CreateUnbounded<IMessage>(new UnboundedChannelOptions { SingleReader = true, AllowSynchronousContinuations = false })` with a single reader loop that spawns concurrent `Task.Run` per message, optionally throttled by `SemaphoreSlim(MaxConcurrency)`.

**Rationale**:
- **Unbounded**: Publisher never blocks. Bounded variant deferred to a future feature.
- **SingleReader = true**: One reader loop (the `BackgroundService`); `Channel<T>` can optimize internal buffers for this. Reading is sequential; concurrency is on the dispatch side (spawned tasks), not the channel read side.
- **AllowSynchronousContinuations = false**: Caller of `TryWrite` returns before any handler runs (satisfies SC-002).
- **Concurrent dispatch via `Task.Run`**: Messages are dispatched concurrently — handlers for different messages run in parallel. `MaxConcurrency = null` means unlimited; `MaxConcurrency = N` limits via `SemaphoreSlim(N, N)`.
- **Spawned tasks use `CancellationToken.None`**: In-flight handlers run to completion on shutdown. `StopAsync` cancels `ReadAllAsync`, stopping new message intake. No new messages are accepted after shutdown begins; in-flight handlers complete naturally.

**`LocalChannelOptions`**:
```
public class LocalChannelOptions
{
    // null = unlimited concurrent handlers; N = max N simultaneous handlers
    public int? MaxConcurrency { get; set; }
}
```

**Lifetime**: `Channel<IMessage>` registered as `Singleton`. `LocalChannelOptions` configured via `IOptions<LocalChannelOptions>`.

**Alternatives considered**:
- `BlockingCollection<T>` — rejected: blocking API, poor async integration.
- `ConcurrentQueue<T>` + timer poll — rejected: polling adds artificial latency.
- `Parallel.ForEachAsync` — rejected: requires materializing batches; doesn't stream from channel naturally.
- Multiple concurrent channel readers — rejected: `SingleReader = true` gives better channel performance; concurrency on the dispatch side (spawned tasks) achieves the same result with cleaner shutdown semantics.

---

## R-004: Transaction detection in `IMessageService<TContext>`

**Decision**: Check `_dbContext.Database.CurrentTransaction != null` to detect an active transaction.

**Rationale**: `TContext` is resolved from the scoped DI container. When `TransactionBehavior` is managing the transaction, it calls `BeginTransactionAsync()` on the `TContext`'s underlying connection. `Database.CurrentTransaction` is non-null during this period. This is the same `TContext` instance shared across the scope.

**Branch logic**:
- `CurrentTransaction != null` → `AddEventAsync` only. `TransactionBehavior` calls `SaveEventsAsync` at the end of the behavior chain — the events added by `IMessageService` are included.
- `CurrentTransaction == null` → `AddEventAsync` + `SaveEventsAsync(null, ct)` immediately. This commits a standalone mini-write within the outbox repository's own connection.

**Alternatives considered**:
- Ambient flag (thread-local/AsyncLocal) set by `TransactionBehavior` — rejected: adds coupling between `TransactionBehavior` and `IMessageService`; `Database.CurrentTransaction` achieves the same with zero coupling.
- Always-standalone (no transaction detection) — rejected: would break atomicity when called inside a TransactionBehavior scope; outbox events would commit before domain data.

---

## R-005: `"local-channel"` and constitution IV compliance

**Decision**: `"local-channel"` is classified as intra-process signaling, not inter-service messaging — exempt from the outbox mandate.

**Rationale**: Constitution IV states "All inter-service and integration messages MUST be written atomically." `"local-channel"` messages:
1. Never leave the process boundary.
2. Are explicitly scoped to non-durable, non-critical scenarios (cache invalidation, lightweight notifications).
3. Mirror the existing `IFireAndForgetNotification` concept already in the framework.

The constitution's intent is to prevent silent message loss at service boundaries. `"local-channel"` is a deliberate, transparent trade-off with no cross-process guarantee needed. `"local"` (outbox-backed) is available for durable in-process delivery.

---

## R-006: Metrics for local publishers

**Decision**: Reuse `DeliveryMetrics.IncrementDeliveryAttempt/Success/Failure/RecordLatency` with publisher key `"local"` or `"local-channel"`.

**Rationale**:
- `"local"` route: delivered via existing `DeliveryProcessor`, which already calls `DeliveryMetrics` — no extra work.
- `"local-channel"` route: `LocalChannelBackgroundService` calls the same `DeliveryMetrics` methods with key `"local-channel"` manually (pattern: `Stopwatch` + try/catch + metric calls matching `DeliveryProcessor`).

This satisfies SC-008 with no new metric infrastructure.
