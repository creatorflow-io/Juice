# Public API Contracts: Policy-Controlled Routing Key

**Feature**: `002-policy-routing-key`
**Generated**: 2026-03-03

---

## Summary of Changes

This feature extends two existing types and introduces one new message header. No interfaces are added or removed. All changes are backward-compatible additive extensions.

---

## `Juice.Messaging.Policies.PublishRoute`

**Package**: `Juice.Messaging`
**Namespace**: `Juice.Messaging.Policies`

### Before (current)

```csharp
public sealed record PublishRoute(string PublisherKey, string Destination);
```

### After

```csharp
public sealed record PublishRoute(
    string PublisherKey,
    string Destination,
    string? RoutingKey = null);
```

**Breaking change**: None — `RoutingKey` is an optional parameter with a `null` default. All existing `new PublishRoute("key", "dest")` callsites compile unchanged.

**Usage by custom `IMessagePublishingPolicy` implementations**:

```csharp
// Returning a fixed routing key
return new[] { new PublishRoute("rabbitmq", "orders-exchange", "orders.placed") };

// Returning a context-driven routing key
var routingKey = $"{context.Domain}.{context.EventType.ToLowerInvariant()}";
return new[] { new PublishRoute("rabbitmq", "topic-exchange", routingKey) };

// Returning no routing key (existing behaviour preserved)
return new[] { new PublishRoute("rabbitmq", "default-exchange") };
```

---

## Configuration: `PublisherDestination`

**Package**: `Juice.Messaging` (internal type, configured via `appsettings.json`)

The `PublisherDestination` configuration object gains an optional `RoutingKey` property. This affects the JSON configuration structure consumed by `DefaultEventPublishingPolicy`.

### New optional field

```json
{
  "Key": "rabbitmq",
  "Destination": "orders-exchange",
  "RoutingKey": "orders.placed"
}
```

**`RoutingKey` omitted** (existing configs): `null` — default derivation from `x-message-name` header is used.
**`RoutingKey` present**: used as the routing key for messages matching this rule.

---

## Message Header: `x-routing-key`

**Set by**: `CompositeEventPublisher` (when policy resolves a non-null/non-empty routing key)
**Consumed by**: `RabbitMQProducer`
**Persisted**: Yes — stored in the outbox `Headers` JSON column, so delivery via `DeliveryProcessor` also respects the policy-configured routing key.

### Routing key selection chain in `RabbitMQProducer`

```
x-routing-key  →  x-message-name  →  x-message-type  →  InvalidOperationException
```

Priority 1 (new): `x-routing-key` — policy-configured override
Priority 2 (existing): `x-message-name` — event's `EventName` property
Priority 3 (existing): `x-message-type` — event's class name

---

## `IMessagePublishingPolicy` (unchanged)

```csharp
public interface IMessagePublishingPolicy
{
    ValueTask<IReadOnlyCollection<PublishRoute>> ResolveAsync(PolicyResolveContext context);
}
```

Custom policy implementations can now populate `RoutingKey` on their returned `PublishRoute` objects. No interface changes are required.

---

## `ITransportPublisher` (unchanged)

```csharp
public interface ITransportPublisher
{
    string Key { get; }
    ValueTask PublishAsync(byte[] payload, PublishContext context,
        CancellationToken cancellationToken = default);
}
```

The routing key is delivered via `context.Headers["x-routing-key"]`, not a new property on `PublishContext`. This keeps transport implementations independent of policy concepts.
