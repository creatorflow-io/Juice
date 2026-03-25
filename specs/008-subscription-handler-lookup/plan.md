# Implementation Plan: Subscriptions Manager Handler Lookup for Local Routes

**Branch**: `008-subscription-handler-lookup` | **Date**: 2026-03-25 | **Spec**: [spec.md](spec.md)
**Input**: Feature specification from `/specs/008-subscription-handler-lookup/spec.md`

## Summary

Extend `Juice.Messaging.Local` so that `ISubscriptionsManager` (already used by RabbitMQ consumers) is also used to register and resolve handlers for integration events published on the `"local"` and `"local-channel"` in-process routes. A new `LocalConsumerBuilder` and `AddLocalConsumer` extension method on `MessagingBuilder` provide the registration API. When the subscriptions manager is populated, `LocalDispatchHelper` uses it instead of scanning DI; when it is absent the existing DI-scan fallback preserves backward compatibility.

---

## Technical Context

**Language/Version**: C# / .NET 6, 8, 9 (multi-target `net6.0;net8.0;net9.0`)
**Primary Dependencies**: `Juice.EventBus` (ISubscriptionsManager, SubscriptionInfo, ISubscriptionsProvider, InMemorySubscriptionsManager), `Juice.Messaging.Local` (LocalDispatchHelper, LocalChannelBackgroundService, LocalTransportPublisher, LocalMessagingBuilderExtensions)
**Storage**: N/A — runtime in-memory singleton; no DB migration required
**Testing**: xUnit, `IgnoreOnCIFact` for infra tests; `[InitializeMessageContext]` on messaging test classes
**Target Platform**: Any .NET host (ASP.NET Core, Worker Service, console)
**Project Type**: NuGet library (`Juice.Messaging.Local`)
**Performance Goals**: No measurable overhead — `ISubscriptionsManager.GetHandlersForEventAsync` is an in-memory dictionary lookup
**Constraints**: No breaking changes to `ISubscriptionsManager` interface; no changes to RabbitMQ paths; keyed DI must work across all target frameworks
**Scale/Scope**: Single library project, ~5 files modified/added, ~2 test classes

---

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-checked after Phase 1 design.*

| Principle | Status | Notes |
|-----------|--------|-------|
| I. Lightweight & Dual-Architecture | ✅ PASS | Feature is opt-in via `AddLocalConsumer`; no forced dependency on subscriptions manager. Fallback keeps monolith/microservice parity. |
| II. Library-First Composability | ✅ PASS | All changes in `Juice.Messaging.Local` (`core/src/`). No new project required — `LocalConsumerBuilder` is a natural addition to the existing library. No new cross-library dependency. |
| III. DDD + CQRS | ✅ PASS | No business logic changes. No pipeline behavior changes. |
| IV. Reliable Messaging via Outbox | ✅ PASS | Outbox write path unchanged. `LocalTransportPublisher` dispatch logic extended (handler lookup only), not bypassed. |
| V. Multi-Tenancy First | ✅ PASS | `ISubscriptionsManager.GetHandlersForEventAsync` returns handler types; `IntegrationEventDispatcher` already passes `TenantId` from the event — no change needed. |

**Post-Design Re-check**: ✅ All principles pass. No violations to justify.

---

## Project Structure

### Documentation (this feature)

```text
specs/008-subscription-handler-lookup/
├── plan.md              ← this file
├── spec.md              ← feature specification
├── research.md          ← Phase 0: all key decisions
├── data-model.md        ← Phase 1: runtime entities & dispatch flow
├── quickstart.md        ← Phase 1: usage guide
├── contracts/
│   └── public-api.md    ← Phase 1: new types & changed signatures
└── checklists/
    └── requirements.md  ← spec quality checklist
```

### Source Code (repository root)

```text
core/src/Juice.Messaging.Local/
├── DependencyInjection/
│   └── LocalMessagingBuilderExtensions.cs   ← ADD AddLocalConsumer() method
├── Internal/
│   ├── LocalConsumerBuilder.cs              ← NEW
│   ├── LocalDispatchHelper.cs               ← MODIFY: add ISubscriptionsManager? param
│   ├── LocalChannelBackgroundService.cs     ← MODIFY: inject & pass ISubscriptionsManager
│   └── LocalTransportPublisher.cs           ← MODIFY: inject & pass ISubscriptionsManager

core/test/Juice.Messaging.Local.Tests/       ← NEW test project (or extend existing)
├── LocalConsumerBuilderTests.cs             ← unit tests
└── LocalChannelDispatchTests.cs             ← integration tests (no infra needed)
```

**Existing files not changed**:
- `Juice.EventBus/Subscriptions/ISubscriptionsManager.cs`
- `Juice.EventBus/Subscriptions/InMemorySubscriptionsManager.cs`
- `Juice.EventBus/Subscriptions/SubscriptionInfo.cs`
- `Juice.EventBus/Subscriptions/ISubscriptionsProvider.cs`
- `Juice.EventBus/Subscriptions/InMemorySubscriptionsProvider.cs`
- `Juice.EventBus.RabbitMQ/**` (all RabbitMQ files)
- `Juice.Messaging/Integrations/IntegrationEventDispatcher.cs`

**Structure Decision**: Single library project `Juice.Messaging.Local`. No new library project justified — the feature adds a builder class and modifies three internal classes within existing project boundaries.

---

## Implementation Steps

### Step 1 — `LocalConsumerBuilder` (new internal class)

**File**: `core/src/Juice.Messaging.Local/Internal/LocalConsumerBuilder.cs`

- Accumulates `SubscriptionInfo` records via `Subscribe<TEvent, THandler>(string? key = null)`.
- Calls `services.TryAddTransient<THandler>()` for each handler.
- `Build()` returns an `InMemorySubscriptionsProvider` wrapping the accumulated list.
- Make it `internal sealed` (only created inside `AddLocalConsumer`).

### Step 2 — `AddLocalConsumer` extension method

**File**: `core/src/Juice.Messaging.Local/DependencyInjection/LocalMessagingBuilderExtensions.cs`

- New `AddLocalConsumer(this MessagingBuilder builder, Action<LocalConsumerBuilder> configure)` method.
- Creates `LocalConsumerBuilder`, calls `configure`, calls `Build()` to get an `ISubscriptionsProvider`.
- Registers the provider with `services.AddSingleton<ISubscriptionsProvider>(provider)`.
- Ensures keyed singleton `ISubscriptionsManager` under key `"local"`:
  ```
  services.TryAddKeyedSingleton<ISubscriptionsManager>("local", (sp, _) =>
      new InMemorySubscriptionsManager(
          sp.GetServices<ISubscriptionsProvider>(),
          sp.GetRequiredService<ILogger<InMemorySubscriptionsManager>>(),
          topicSupport: false));
  ```
  Use `TryAdd` to prevent double-registration if `AddLocalConsumer` is called more than once. Note: multiple calls to `AddLocalConsumer` each register an `ISubscriptionsProvider`; the singleton manager picks up all of them because `InMemorySubscriptionsManager` accepts `IEnumerable<ISubscriptionsProvider>`.

### Step 3 — Modify `LocalDispatchHelper`

**File**: `core/src/Juice.Messaging.Local/Internal/LocalDispatchHelper.cs`

Change `DispatchIntegrationEventAsync` signature to accept `ISubscriptionsManager? subscriptionsManager`:

```csharp
public static async Task<EventDispatchResult> DispatchIntegrationEventAsync(
    IServiceProvider serviceProvider,
    IntegrationEventDispatcher dispatcher,
    ISubscriptionsManager? subscriptionsManager,   // NEW
    IIntegrationEvent evt,
    CancellationToken cancellationToken)
{
    List<Type> handlerTypes;

    if (subscriptionsManager != null)
    {
        var types = await subscriptionsManager.GetHandlersForEventAsync(evt.GetType().Name);
        handlerTypes = types.ToList();
    }
    else
    {
        // Backward-compatible fallback: open DI scan
        var handlerType = typeof(IIntegrationEventHandler<>).MakeGenericType(evt.GetType());
        handlerTypes = serviceProvider.GetServices(handlerType)
            .Select(h => h!.GetType()).ToList();
    }

    var source = MessageContext.IsInitialized
        ? MessageContext.Current.Source ?? string.Empty
        : string.Empty;

    var context = new EventDispatchContext(
        handlerTypes, evt.GetType().Name, evt.TenantId, source);

    return await dispatcher.DispatchAsync(evt, context);
}
```

### Step 4 — Modify `LocalChannelBackgroundService`

**File**: `core/src/Juice.Messaging.Local/Internal/LocalChannelBackgroundService.cs`

- Add constructor parameter `[FromKeyedServices("local")] ISubscriptionsManager? subscriptionsManager = null`.
- Store in `_subscriptionsManager` field.
- Pass `_subscriptionsManager` to `LocalDispatchHelper.DispatchIntegrationEventAsync(...)`.

### Step 5 — Modify `LocalTransportPublisher`

**File**: `core/src/Juice.Messaging.Local/Internal/LocalTransportPublisher.cs`

- Add constructor parameter `[FromKeyedServices("local")] ISubscriptionsManager? subscriptionsManager = null`.
- Store in `_subscriptionsManager` field.
- Pass `_subscriptionsManager` to `LocalDispatchHelper.DispatchIntegrationEventAsync(...)`.

### Step 6 — Tests

**File**: `core/test/Juice.Messaging.Local.Tests/LocalConsumerBuilderTests.cs`

Unit tests (no external infra):
1. `Subscribe_registers_handler_in_subscriptions_manager_Async` — verify `GetHandlersForEventAsync` returns the registered type after `AddLocalConsumer`.
2. `Subscribe_is_idempotent_for_same_event_handler_pair_Async` — duplicate call does not double-register.
3. `Multiple_handlers_for_same_event_are_all_returned_Async` — two handlers, both returned.

**File**: `core/test/Juice.Messaging.Local.Tests/LocalChannelDispatchTests.cs`

Integration tests (in-memory, no RabbitMQ/DB needed):
1. `Local_channel_dispatches_to_registered_handler_via_subscriptions_manager_Async` — publish on local-channel, verify handler invoked.
2. `Local_channel_does_not_invoke_unregistered_handler_Async` — handler in DI but not in subscriptions manager → not invoked.
3. `Local_channel_fallback_to_di_scan_when_no_subscriptions_manager_Async` — without `AddLocalConsumer`, existing DI-scan behavior is preserved.
4. `Dispatch_result_is_not_handled_when_no_handlers_registered_Async` — no handlers in manager → result is `NotHandled`.

---

## Risk Register

| Risk | Likelihood | Impact | Mitigation |
|------|-----------|--------|------------|
| `InMemorySubscriptionsManager` internal constructor not accessible from `Juice.Messaging.Local` | Low | High | Both projects are in same solution; use `InternalsVisibleTo` or make constructor internal + visible, matching the existing pattern in `Juice.EventBus` |
| Multiple `AddLocalConsumer` calls create multiple `ISubscriptionsProvider` — manager sees all or only first | Low | Medium | `IEnumerable<ISubscriptionsProvider>` in constructor; all providers are collected — multiple calls are additive. Covered by test. |
| Keyed nullable DI injection (`[FromKeyedServices("local")] ISubscriptionsManager? = null`) not supported on target frameworks | Low | Medium | Nullable keyed services are supported in .NET 8+ via `GetKeyedService<T>()`. For net6 targets, use `serviceProvider.GetKeyedService<ISubscriptionsManager>("local")` resolution pattern instead of constructor injection, or guard with try-catch. |

---

## Definition of Done

- [ ] `LocalConsumerBuilder` created in `Juice.Messaging.Local`.
- [ ] `AddLocalConsumer` extension registered and tested.
- [ ] `LocalDispatchHelper` modified with `ISubscriptionsManager?` parameter.
- [ ] `LocalChannelBackgroundService` injects and passes subscriptions manager.
- [ ] `LocalTransportPublisher` injects and passes subscriptions manager.
- [ ] All new unit tests pass without external infra.
- [ ] Fallback (no `AddLocalConsumer`) behavior verified by test.
- [ ] No existing tests broken.
- [ ] No breaking change to `ISubscriptionsManager` public interface.
