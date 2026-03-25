# Research: Subscriptions Manager Handler Lookup for Local Routes

**Feature**: 008-subscription-handler-lookup
**Date**: 2026-03-25

---

## Decision 1: Shared or separate subscriptions manager for local routes?

**Decision**: One shared `ISubscriptionsManager` singleton, keyed `"local"`, serves both `"local"` and `"local-channel"` routes.

**Rationale**: Both routes dispatch to the same in-process handlers. There is no use case where the same event type would need different handlers on local vs local-channel for the same application instance. A single registry is simpler and avoids duplication. Parity with RabbitMQ (which uses a keyed singleton per consumer) is maintained by using the same keyed DI pattern with the well-known key `"local"`.

**Alternatives considered**:
- *Separate managers per route* — unnecessary complexity; the spec has one subscription registry for both routes.
- *Unkeyed singleton* — conflicts with future extensions and is harder to distinguish from RabbitMQ managers in DI diagnostics.
- *Non-keyed interface extension* — would require adding a new interface `ILocalSubscriptionsManager`, which violates YAGNI (the existing `ISubscriptionsManager` covers all needed operations).

---

## Decision 2: Replace DI scan or augment it?

**Decision**: Replace the DI scan with the subscriptions manager when it is registered. Fall back to the existing DI scan when no subscriptions manager is registered (i.e., `AddLocalConsumer` was never called).

**Rationale**: The spec requires that only explicitly registered handlers are invoked (SC-002). Replacing the DI scan achieves this. The fallback to DI scan when no manager exists preserves backward compatibility for applications that do not yet call `AddLocalConsumer` — they keep the current open-discovery behavior unchanged.

**Alternatives considered**:
- *Always replace, no fallback* — breaks all existing local-route consumers on upgrade; violates Lightweight principle (forces every app to add `AddLocalConsumer` calls).
- *Always augment (union of both)* — violates SC-002 ("no unregistered handler is invoked"); creates surprising behavior.
- *Feature flag to opt-in* — unnecessary complexity; presence of the keyed `ISubscriptionsManager` is a sufficient signal.

---

## Decision 3: Where is `ISubscriptionsManager` registered for local routes?

**Decision**: Registered in `AddLocalConsumer(Action<LocalConsumerBuilder>)` on `MessagingBuilder`, as a keyed singleton under key `"local"`. Uses the existing `InMemorySubscriptionsManager` with an `ISubscriptionsProvider` populated by `LocalConsumerBuilder`.

**Rationale**: Mirrors the `RabbitMQConsumerBuilder` pattern exactly — `SubscriptionBuilder` gathers `SubscriptionInfo` records; `InMemorySubscriptionsProvider` adapts them; `InMemorySubscriptionsManager` consumes the provider. No new abstractions required. Re-uses `topicSupport: false` (exact-name matching only for local routes, as scoped in the spec's Assumptions).

**Alternatives considered**:
- *New `ILocalSubscriptionsProvider` subtype* — unnecessary; `ISubscriptionsProvider` is already generic enough.
- *Direct `AddSubscription` calls at startup via `IHostedService`* — imperative, harder to test in isolation.

---

## Decision 4: How does `LocalDispatchHelper` receive the subscriptions manager?

**Decision**: `LocalDispatchHelper.DispatchIntegrationEventAsync` gains an `ISubscriptionsManager?` parameter. Callers (`LocalChannelBackgroundService`, `LocalTransportPublisher`) inject the keyed `ISubscriptionsManager` with key `"local"` and pass it through. If the resolved service is `null` (not registered), the helper falls back to DI scan.

**Rationale**: `LocalDispatchHelper` is an `internal static` class — it has no DI injection. Passing the manager as a parameter keeps the helper stateless and directly testable. `LocalChannelBackgroundService` and `LocalTransportPublisher` both already inject services from DI; adding one more keyed injection is minimal.

**Alternatives considered**:
- *Make `LocalDispatchHelper` non-static and inject `ISubscriptionsManager`* — heavier refactor than needed; the static pattern is idiomatic in this codebase.
- *Resolve from `IServiceProvider` inside the helper* — hides the dependency, harder to test.

---

## Decision 5: Builder API design — `LocalConsumerBuilder`

**Decision**: New `LocalConsumerBuilder` class in `Juice.Messaging.Local`, mirroring `RabbitMQConsumerBuilder`. Exposes `Subscribe<TEvent, THandler>(string? key = null)` which (a) adds a `SubscriptionInfo` and (b) registers `THandler` as transient in DI.

**Rationale**: Consistent API across all consumer routes. Developers already familiar with `RabbitMQConsumerBuilder.Subscribe<>()` will find the same pattern here. Single method call handles both subscription registry AND DI registration.

**Alternatives considered**:
- *Extend `MessagingBuilder` with `SubscribeLocal<TEvent, THandler>()`* — flat, no builder nesting; harder to group related subscriptions.
- *Reuse `SubscriptionBuilder` directly* — `SubscriptionBuilder` doesn't register handlers in DI; a thin wrapper would still be needed.

---

## Decision 6: Breaking-change scope

**Decision**: This is a behavioral change (not an interface-breaking change) for applications that call `AddLocalConsumer`. The `ISubscriptionsManager` interface is unchanged. `LocalDispatchHelper`'s signature changes, but it is `internal`.

**Rationale**: No public API surface changes. Existing applications that don't call `AddLocalConsumer` are unaffected (fallback to DI scan). Applications that do call `AddLocalConsumer` get the new explicit-registration behavior. No MAJOR version bump required for the library itself (behavior opt-in, not forced).

**Alternatives considered**:
- *Force MAJOR bump* — overly disruptive; the change is opt-in via the new builder method.
