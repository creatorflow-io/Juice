# Data Model: Policy-Controlled Routing Key

**Feature**: `002-policy-routing-key`
**Generated**: 2026-03-03

This feature adds no new entities or database tables. It extends two existing value types and introduces one new message header.

---

## Modified Value Types

### `PublishRoute` (extended)

Represents the resolved destination for a single publish operation returned by `IMessagePublishingPolicy`.

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `PublisherKey` | `string` | Yes | Identifies the transport publisher (e.g., `"rabbitmq"`) |
| `Destination` | `string` | Yes | The target exchange / topic endpoint name |
| `RoutingKey` | `string?` | No (default: `null`) | Optional routing key override. When `null` or empty, the transport uses the default event-name derivation. |

**Invariants**:
- `PublisherKey` and `Destination` are non-null; `RoutingKey` is optional.
- A null or empty-string `RoutingKey` is semantically equivalent to absent — the transport falls back to the default derivation.

---

### `PublisherDestination` (extended, internal config type)

Maps a publisher key to a destination and optional routing key in the `PublishingPolicyOptions` configuration model.

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `Key` | `string` | Yes | Publisher key (matches `ITransportPublisher.Key`) |
| `Destination` | `string` | Yes | Exchange/topic name |
| `RoutingKey` | `string?` | No (default: `null`) | Optional routing key override for this destination |

---

## Message Headers

### New Header: `x-routing-key`

Added to the message headers dictionary when the publishing policy specifies a routing key.

| Header | Set by | Consumed by | Semantics |
|--------|--------|-------------|-----------|
| `x-routing-key` | `CompositeEventPublisher` | `RabbitMQProducer` | Policy-specified routing key override. Takes precedence over `x-message-name` and `x-message-type` in the transport routing key derivation chain. |

**Header lifecycle**:
1. `CompositeEventPublisher` sets `x-routing-key` in the headers dictionary if `route.RoutingKey` is non-null/non-empty.
2. Headers (including `x-routing-key` when present) are persisted in the outbox `Headers` JSON column atomically with the domain transaction.
3. `DeliveryProcessor` reads stored headers and passes them through `PublishContext.Headers` to the transport.
4. `RabbitMQProducer` reads `x-routing-key` first in its routing key selection chain.

**Existing headers (unchanged)**:

| Header | Description |
|--------|-------------|
| `x-message-name` | Event's canonical name (`IEvent.EventName`, usually class name). Default routing key source. |
| `x-message-type` | Event's class name. Secondary fallback for routing key. |
| `x-original-routing-key` | Diagnostic: records the actual routing key used for delivery (set by `RabbitMQProducer`). |
| `x-original-exchange` | Diagnostic: records the exchange used for delivery. |

---

## Routing Key Selection Chain (updated)

```
1. x-routing-key header (from policy, new)
2. x-message-name header (event's EventName, existing default)
3. x-message-type header (event's class name, existing fallback)
4. Error: missing required header
```

---

## Configuration Schema (updated)

The `PublisherDestination` object in `appsettings.json` gains an optional `RoutingKey` field:

```json
{
  "Messaging": {
    "Publishing": {
      "Default": {
        "Publishers": [
          { "Key": "rabbitmq", "Destination": "default_exchange" }
        ]
      },
      "Rules": [
        {
          "Priority": 10,
          "Match": { "Event": "OrderPlacedEvent" },
          "Publishers": [
            {
              "Key": "rabbitmq",
              "Destination": "orders-exchange",
              "RoutingKey": "orders.placed"
            }
          ]
        }
      ]
    }
  }
}
```

Existing configuration files without `RoutingKey` are unaffected — the field defaults to `null`, preserving current behaviour.
