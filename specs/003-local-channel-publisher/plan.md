# Implementation Plan: Local Transport Publishers with Unified Message Service

**Branch**: `003-local-channel-publisher` | **Date**: 2026-03-14 | **Spec**: [spec.md](./spec.md)

## Summary

Introduce `IMessageService` / `IMessageService<TContext>` as a unified application-layer publishing interface in `Juice.Messaging`, add a `Juice.Messaging.Local` library providing two in-process transport publishers (`"local-channel"` and `"local"`) and the channel-drain background service. `"local-channel"` short-circuits directly to an in-memory channel (zero DB). `"local"` writes to the outbox and is delivered by the existing `DeliveryHostedService` via a new keyed `ITransportPublisher` that dispatches to in-process handlers.

## Technical Context

**Language/Version**: C# / .NET 6, 8, 9 (multi-targeted: `net6.0;net8.0;net9.0`)
**Primary Dependencies**: `Juice.Messaging`, `Juice.MediatR`, `Juice.EventBus`, `Juice.Messaging.Outbox`
**Storage**: No new DB schema — reuses `OutboxEvents` / `OutboxDeliveries` tables
**Testing**: xUnit + `IgnoreOnCIFact`, `[InitializeMessageContext]` (existing patterns)
**Target Platform**: Library — consumed by ASP.NET Core host projects
**Project Type**: NuGet library (`core/src/Juice.Messaging.Local`)
**Performance Goals**: `"local-channel"` dispatch overhead ≤ cost of a `Channel.Writer.TryWrite` call; no polling latency for in-memory delivery
**Constraints**: No new EF migrations; no new DB tables; no circular dependencies in layer graph

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-checked after Phase 1 design.*

| Principle | Status | Notes |
|---|---|---|
| I. Lightweight & Dual-Architecture | ✅ Pass | `IMessageService` works identically in monolith and microservice mode. Local publishers are opt-in via configuration. |
| II. Library-First Composability | ✅ Pass | New `core/src/Juice.Messaging.Local` with clear boundary: bridges MediatR + Messaging. Test in `core/test/Juice.Messaging.Local.Tests`. No circular deps. |
| III. DDD + CQRS | ✅ Pass | Domain events still use `[Domain("X")]`. `IMediator` dispatch preserved. No behavior order changes. |
| IV. Reliable Messaging via Outbox | ⚠️ Justified Violation | `"local-channel"` bypasses outbox. See Complexity Tracking. |
| V. Multi-Tenancy First | ✅ Pass | Tenant context flows via `ITenantAccessor` into `PolicyResolveContext`; `MessageContext` initialized per existing patterns. |

## Complexity Tracking

| Violation | Why Needed | Simpler Alternative Rejected Because |
|---|---|---|
| `"local-channel"` bypasses outbox (Constitution IV) | Enables zero-DB-overhead in-process dispatch for non-durable events (cache invalidation, lightweight notifications). Framework already has `IFireAndForgetNotification` as a fire-and-forget concept. `"local-channel"` extends this to the policy routing layer. | Requiring outbox for all messages adds DB write + poll latency to events that explicitly don't need at-least-once delivery. Violates Principle I (lightweight). `"local"` route is available when durability IS needed. |

## Project Structure

### Documentation (this feature)

```
specs/003-local-channel-publisher/
├── plan.md              ← this file
├── research.md          ← Phase 0 output
├── data-model.md        ← Phase 1 output
├── contracts/           ← Phase 1 output
│   ├── IMessageService.md
│   └── IMessageService-TContext.md
└── tasks.md             ← /speckit.tasks output
```

### Source Code

```
core/src/Juice.Messaging/
│   (existing — add interfaces only)
│   ├── IMessageService.cs              ← NEW: non-generic interface
│   └── IMessageService`1.cs           ← NEW: generic interface IMessageService<TContext>

core/src/Juice.Messaging.Local/        ← NEW PROJECT
├── Assembly.cs
├── Internal/
│   ├── MessageService.cs              ← implements IMessageService (local-channel only)
│   ├── MessageServiceT.cs             ← implements IMessageService<TContext> (all routes)
│   ├── LocalChannelBackgroundService.cs ← drains Channel<IMessage>, dispatches handlers
│   └── LocalTransportPublisher.cs     ← ITransportPublisher keyed "local"
└── DependencyInjection/
    └── LocalMessagingBuilderExtensions.cs

core/test/Juice.Messaging.Local.Tests/ ← NEW TEST PROJECT
├── LocalChannelTests.cs
├── LocalTransportPublisherTests.cs
├── MessageServiceTests.cs
└── IdempotencyDeduplicationTests.cs
```

**Structure Decision**: Single new library `Juice.Messaging.Local` at the Messaging layer, plus interface additions to the existing `Juice.Messaging`. Interfaces live in `Juice.Messaging` (lightweight, no new deps). Implementations and publishers live in `Juice.Messaging.Local` (references `Juice.MediatR` + `Juice.EventBus`).

---

## Phase 0: Research

*See [research.md](./research.md) for full findings.*

### Key Decisions

| Decision | Choice | Rationale |
|---|---|---|
| Runtime dispatch of `INotification` | Reflection-cached `MethodInfo` on `INotificationPublisher.Publish<T>` | Same pattern as `IntegrationEventDispatcher` for `HandleAsync`; O(1) cache lookup after warmup |
| `IIntegrationEvent` handler discovery | Resolve `IEnumerable<IIntegrationEventHandler<T>>` from DI scope | No `ISubscriptionsManager` registration needed; consistent with Q1 clarification |
| Channel capacity | Unbounded `Channel.CreateUnbounded<IMessage>()` with `SingleReader = true` | Matches spec assumption; bounded variant deferred |
| Concurrent dispatch | Single reader loop + `SemaphoreSlim(MaxConcurrency)` spawning concurrent `Task.Run` per message | Separates reading from handling; semaphore controls in-flight count; `MaxConcurrency = null` means unlimited |
| Transaction detection in `IMessageService<TContext>` | Check `TContext.Database.CurrentTransaction != null` | Direct, no additional abstractions; `TContext` is already a scoped dep |
| Interface locations | `IMessageService` + `IMessageService<TContext>` in `Juice.Messaging` | Keeps contracts in the right layer; implementations in `Juice.Messaging.Local` |
| `IMessageService<TContext>` generic constraint | `where TContext : class` (no EF dep on the interface) | Keeps `Juice.Messaging` free of EF Core dependency |

---

## Phase 1: Design

### Dispatch Flow Diagram

```
IMessageService.PublishAsync(IMessage msg)
  │
  ├─ ResolveAsync(PolicyResolveContext)  → routes[]
  │
  ├─ [foreach route with key "local-channel"]
  │     Channel<IMessage>.Writer.TryWrite(msg)    ← zero DB, returns immediately
  │
  └─ [foreach route with key "local" or broker]  ← only IMessageService<TContext>
        IOutboxService<TContext>.AddEventAsync(msg)
        IOutboxService<TContext>.SaveEventsAsync(null, ct)
        │
        └─ [if any route has key "local" AND no "local-channel" route already enqueued]
              Channel<IMessage>.Writer.TryWrite(msg)  ← immediate best-effort dispatch
              │                                         (idempotency deduplicates with
              │                                          delivery retry path below)

LocalChannelBackgroundService (hosted)
  Channel<IMessage>.Reader.ReadAllAsync()
    │
    ├─ is INotification? → INotificationPublisher.Publish<T>(notification)  [reflected]
    └─ is IIntegrationEvent? → resolve handlers from DI
         → IntegrationEventDispatcher.DispatchAsync()
              └─ IIdempotencyService.TryCreateRequest(EventName, "{Source}:{MessageId}")
                   → Succeeded: invoke handlers
                   → Failed (duplicate): skip → EventDispatchResult.Duplicated

DeliveryHostedService (existing, "local" publisher key)
  OutboxDelivery[PublisherKey="local"]
    └─ LocalTransportPublisher.PublishAsync(byte[], PublishContext)
         ├─ if !MessageContext.IsInitialized:
         │     Initialize from headers (x-source, x-correlation-id, x-causation-id)
         │     ← ensures idempotency key matches immediate dispatch path
         │
         IMessageSerializer.Deserialize(bytes, typeName)
           ├─ is INotification? → INotificationPublisher.Publish<T>()  [reflected]
           └─ is IIntegrationEvent? → resolve handlers from DI
                → IntegrationEventDispatcher.DispatchAsync()
                     └─ IIdempotencyService.TryCreateRequest(...)
                          → already exists from immediate dispatch → Duplicated (skip)
         │
         └─ finally: clear MessageContext if initialized here
```

### Dependency Graph (new project)

```
Juice.Contracts
  └── Juice.Messaging.Contracts
        └── Juice.Messaging          ← add IMessageService, IMessageService<TContext>
              ├── Juice.MediatR
              └── Juice.EventBus     ← ITransportPublisher
                    └── Juice.Messaging.Local  ← NEW (implementations)
```

### DI Registration (new extension methods on `MessagingBuilder`)

```
messaging
  .AddLocalChannel()           → registers: Channel<IMessage> (singleton, unbounded, SingleReader=true)
                                             LocalChannelOptions (configures MaxConcurrency, default=null=unlimited)
                                             LocalChannelBackgroundService (hosted)
                                             MessageService as IMessageService (scoped)

  .AddLocalPublisher()         → registers: LocalTransportPublisher as keyed ITransportPublisher "local" (scoped)
                                             DeliveryHostedService<TContext> wired to "local" publisher (existing mechanism)

  .AddMessageService<TContext>() → registers: MessageService<TContext> as IMessageService<TContext> (scoped)
                                              (implies AddLocalChannel if not already registered)
```

### Critical Implementation Notes

**`LocalTransportPublisher.PublishAsync(byte[] payload, PublishContext context)`**
1. If `MessageContext` is not initialized, restore from outbox headers (`x-source`, `x-correlation-id`, `x-causation-id`) — ensures consistent idempotency key across dispatch paths
2. Extract type name from `context.Headers["x-message-type"]`
3. Resolve assembly type via `IMessageSerializer.Deserialize(payload, typeName)` → `IMessage`
4. Determine dispatch path:
   - `msg is INotification` → reflection-invoke `INotificationPublisher.Publish<T>(msg, ct)`
   - `msg is IIntegrationEvent` → build `EventDispatchContext` from DI-resolved handler types → `IntegrationEventDispatcher.DispatchAsync(evt, ctx)`
5. If handler dispatch returns `EventDispatchResult.Failure`, throw `InvalidOperationException` so `DeliveryProcessor` marks delivery as `Failed` and schedules retry
6. Emit metrics via `LocalChannelMetrics` with publisher key `"local"`
7. In `finally` block: clear `MessageContext` if it was initialized in step 1

**`LocalChannelBackgroundService`** — single reader loop, concurrent dispatch:
```
ExecuteAsync(ct):
  semaphore = MaxConcurrency > 0 ? new SemaphoreSlim(MaxConcurrency) : null

  await foreach msg in Channel.Reader.ReadAllAsync(ct):
    if semaphore != null: await semaphore.WaitAsync(ct)
    _ = Task.Run(async () =>
        try:
          using scope = scopeFactory.CreateScope()
          [dispatch msg via INotificationPublisher or IntegrationEventDispatcher]
          DeliveryMetrics.IncrementDeliverySuccess("local-channel")
        catch ex:
          log error
          DeliveryMetrics.IncrementDeliveryFailure("local-channel", ex.Type)
        finally:
          semaphore?.Release()
    , CancellationToken.None)   ← handlers run to completion even on shutdown
```

- `MaxConcurrency = null` (default) → unlimited concurrent handlers (no semaphore)
- `MaxConcurrency = N` → at most N handlers execute simultaneously
- Spawned tasks use `CancellationToken.None` so in-flight handlers complete on shutdown
- `StopAsync` cancels `ReadAllAsync` → no new messages accepted; in-flight tasks complete naturally

**`MessageService<TContext>.PublishAsync`** routing and transaction branching:
- Resolve routes via `IMessagePublishingPolicy`; classify into `hasLocalChannel`, `hasLocalOutbox`, `hasOutboxRoutes`
- `"local-channel"` routes always short-circuit to `Channel` regardless of transaction state
- For outbox routes: `AddEventAsync` + `SaveEventsAsync(null, ct)` (OutboxRepository joins ambient transaction when present)
- After outbox save, if `hasLocalOutbox && !hasLocalChannel`: enqueue the message to the channel for immediate best-effort dispatch. The `!hasLocalChannel` guard prevents double-enqueueing when both routes are present.
- Immediate dispatch failures are silently absorbed — the outbox guarantees eventual delivery via `DeliveryHostedService`
- Idempotency in `IntegrationEventDispatcher` prevents duplicate handler invocation when both the immediate dispatch and the delivery retry fire for the same message
