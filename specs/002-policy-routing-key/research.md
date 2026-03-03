# Research: Policy-Controlled Routing Key

**Feature**: `002-policy-routing-key`
**Generated**: 2026-03-03

---

## Decision 1: Where to carry the routing key through the publish pipeline

**Decision**: Store the policy-configured routing key as a standard message header (`x-routing-key`) rather than adding a new field to `PublishContext` or `OutboxDelivery`.

**Rationale**:
- `DeliveryProcessor.PublishEventAsync` already passes the full headers dict from `OutboxEvent.Headers` through `PublishContext.Headers` into `RabbitMQProducer`. Headers are persisted atomically with the outbox record. Adding `x-routing-key` to headers at publish time means it flows through both the direct-publish path and the outbox-delivery path without any schema change.
- Adding a dedicated `RoutingKey` column to `OutboxDelivery` would require EF migrations for SQL Server and PostgreSQL — significant scope increase not justified for what is effectively a routing hint.
- Message headers are already the established channel for transport metadata (see `x-message-name`, `x-tenant-id`, `x-correlation-id`, `x-original-routing-key`). A new `x-routing-key` header is consistent with this pattern.

**Alternatives considered**:
- **Add `RoutingKey` to `PublishContext`**: Cleaner for the direct path but requires `DeliveryProcessor` to read and set it from the stored headers on the outbox-delivery path — adding complexity and still needing the header as an intermediary.
- **Add `RoutingKey` column to `OutboxDelivery`**: Most explicit but requires EF migrations for two database providers, a larger scope change with no architectural benefit over the header approach.
- **Add `RoutingKey` to `PublishContext` AND store in header**: Redundant — the header alone is sufficient.

---

## Decision 2: Where to set the `x-routing-key` header

**Decision**: Set `x-routing-key` in `CompositeEventPublisher.PublishAsync` when `route.RoutingKey` is non-null and non-empty, before calling `publisher.PublishAsync`.

**Rationale**:
- `CompositeEventPublisher` is the single point where the resolved `PublishRoute` is converted to a `PublishContext` + headers. It already sets all other contextual headers (`x-correlation-id`, `x-message-name`, `x-tenant-id`). Adding the routing key here keeps all header-assembly logic in one place.
- The header is set only when the policy specifies a routing key — no impact on the existing default path.

**Alternatives considered**:
- **Set in `RabbitMQProducer`**: Producer should not know about policies — it is a transport abstraction.
- **Set in the policy itself**: The policy returns a `PublishRoute`; header assembly is a publisher concern.

---

## Decision 3: How `RabbitMQProducer` selects the routing key

**Decision**: Extend the routing key derivation chain: `x-routing-key` header → `x-message-name` header → `x-message-type` header → error.

**Rationale**:
- The existing fallback chain (`x-message-name` → `x-message-type` → error) is preserved verbatim. The new `x-routing-key` check is prepended — highest priority, fully optional.
- Null/empty `x-routing-key` skips to the next fallback, satisfying FR-007 (empty = absent).
- No change to the existing `x-original-routing-key` diagnostic header — it records whatever routing key was actually used (set after the routing key is resolved).

**Alternatives considered**:
- **Use `PublishContext.RoutingKey` instead of a header**: Requires adding a new property to `PublishContext` and threading it through `DeliveryProcessor` — two files changed vs. one header check.
- **Override inside `CompositeEventPublisher` by setting `x-message-name`**: Misleading — `x-message-name` is the event's canonical name; overwriting it hides the event identity and breaks consumers relying on that header for routing or logging.

---

## Decision 4: Extending `PublishRoute` — positional parameter vs. init property

**Decision**: Add `string? RoutingKey = null` as an optional third positional parameter to the `PublishRoute` record.

**Rationale**:
- All existing callsites (`new PublishRoute("key", "dest")`) compile unchanged because the parameter is optional with a `null` default.
- The single callsite (`DefaultEventPublishingPolicy.Map`) can be updated to `new PublishRoute(p.Key, p.Destination, p.RoutingKey)`.
- Consistent with how `PublishContext` uses positional parameters with defaults.

**Alternatives considered**:
- **`init`-only property**: Requires object-initializer syntax at callsites; no benefit over optional positional parameter for a small record.

---

## Decision 5: Breaking-change assessment

**Decision**: This is a MINOR additive change — no major version bump required.

**Rationale**:
- `PublishRoute` gains a new optional parameter (default `null`) — existing positional constructors compile unchanged.
- `PublisherDestination` gains a new optional property `RoutingKey` — existing configuration files and object-initializer code compile unchanged (property defaults to `null` when absent).
- No existing interface signatures change.
- The routing key derivation change in `RabbitMQProducer` is backward compatible: when `x-routing-key` is absent, behaviour is identical to today.

---

## Affected Files Summary

| File | Change Type | Reason |
|------|-------------|--------|
| `core/src/Juice.Messaging/Policies/PublishRoute.cs` | Minor extension | Add optional `RoutingKey` parameter |
| `core/src/Juice.Messaging/Policies/Internal/PublishingPolicyOptions.cs` | Minor extension | Add `RoutingKey` to `PublisherDestination` |
| `core/src/Juice.Messaging/Policies/Internal/DefaultEventPublishingPolicy.cs` | Update | Propagate `RoutingKey` when building `PublishRoute` |
| `core/src/Juice.EventBus/Internal/CompositeEventPublisher.cs` | Update | Set `x-routing-key` header from route when non-null |
| `core/src/Juice.EventBus.RabbitMQ/Publishing/RabbitMQProducer.cs` | Update | Check `x-routing-key` header first in routing key derivation |
| `core/test/Juice.EventBus.Tests/PublishPoliciesTest.cs` | New tests | Validate routing key in resolved routes |

No database migrations required. No new projects. No new interfaces.
