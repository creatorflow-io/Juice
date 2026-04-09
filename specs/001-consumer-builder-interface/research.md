# Research: Consumer Builder Interface

**Feature**: 001-consumer-builder-interface
**Date**: 2026-04-09

---

## Decision 1: Assembly placement for `IConsumerBuilder`

**Decision**: Place `IConsumerBuilder` in `Juice.EventBus` (`core/src/Juice.EventBus/`).

**Rationale**:
- Both `Juice.EventBus.RabbitMQ` and `Juice.Messaging.Local` already have a direct project reference to `Juice.EventBus`. Adding the interface there requires **zero new project references**.
- `Juice.EventBus` is the existing home for subscription-management abstractions (`ISubscriptionsProvider`, `ILocalSubscriptionsProvider`, `ISubscriptionsManager`) — `IConsumerBuilder` is a natural peer.
- `Juice.EventBus` has no dependency on either builder project, so there is no circular-dependency risk.

**Alternatives considered**:
- *New `Juice.Messaging.Abstractions` project*: Rejected — would add a new project for a single interface when an existing assembly already fits the layering requirements perfectly (Constitution Principle II: distinct abstraction boundary required).
- *`Juice.Messaging.Contracts`*: Rejected — that assembly holds pure event/message contracts (`IIntegrationEvent`, `IIntegrationEventHandler<T>`); builder configuration contracts belong one layer up.

---

## Decision 2: Handling the return-type mismatch (concrete vs interface)

**Decision**: Keep the existing concrete `Subscribe<TEvent, THandler>()` methods returning their concrete builder type (for unbroken fluent chaining). Add an **explicit interface implementation** on each builder that delegates to the concrete method and returns `IConsumerBuilder`.

```csharp
// Explicit interface implementation (added to each builder)
IConsumerBuilder IConsumerBuilder.Subscribe<TEvent, THandler>(string? route)
    => Subscribe<TEvent, THandler>(route);
```

**Rationale**:
- C# does not support covariant return types on interface implementations (only on `override` in a class hierarchy). The explicit implementation pattern is the standard workaround.
- Existing call sites that hold a concrete builder type continue to compile unchanged and receive the concrete return type.
- Call sites that accept `IConsumerBuilder` get a correctly-typed `IConsumerBuilder` return, enabling fluent chaining via the interface.

**Alternatives considered**:
- *Change concrete method return type to `IConsumerBuilder`*: Rejected — breaks existing fluent chains on concrete types without a cast.
- *Add a second overload returning `IConsumerBuilder`*: Rejected — two identically-named methods with the same signature (differing only in return type) are not permitted in C#.

---

## Decision 3: Interface parameter name

**Decision**: Use `route` as the parameter name in `IConsumerBuilder.Subscribe<TEvent, THandler>(string? route = default)`.

**Rationale**:
- `RabbitMQConsumerBuilder` already uses `route`; `LocalConsumerBuilder` uses `key`.
- `route` is the more transport-agnostic term (both builders ultimately use this value as a routing/topic hint).
- Concrete builder parameters retain their own names (`route`, `key`) since parameter name differences between an interface and its implementation are allowed in C#.

---

## Decision 4: Constraints on `IConsumerBuilder`

**Decision**: Interface method carries the same generic constraints as the concrete implementations:
```
where TEvent : IIntegrationEvent
where THandler : class, IIntegrationEventHandler<TEvent>
```

**Rationale**: These constraints are enforced today by both builders and are required for correct handler resolution. Relaxing them in the interface would allow callers to register invalid subscriptions.

---

## Resolved Unknowns

| Unknown | Resolution |
|---------|-----------|
| Which assembly hosts `IConsumerBuilder`? | `Juice.EventBus` |
| How to reconcile concrete vs interface return type? | Explicit interface implementation |
| What parameter name to use in the interface? | `route` |
| Are the builders sealed (preventing subclass approach)? | Yes — both are `sealed`; direct interface implementation only |
| Does `LocalConsumerBuilder` use `key` vs `route`? | Yes — difference is benign; C# allows parameter name divergence |
