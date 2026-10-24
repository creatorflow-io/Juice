# Contract: Configuration Schema

Configuration is optional — everything is also settable in code (FR-019). Sections are bound by the caller (`configuration.GetSection(...)`), exactly like RabbitMQ.

## Connection (`AddConnection(name, section)`)

```json
{
  "Juice": {
    "EventBus": {
      "Connections": {
        "Nats": {
          "Url": "nats://nats-1:4222,nats://nats-2:4222",
          "Name": "orders-api",
          "CredsFile": "/run/secrets/orders.creds",
          "Tls": { "Mode": "Require", "CaFile": "/etc/ssl/nats-ca.pem" },
          "ConnectTimeout": "00:00:02",
          "ReconnectWaitMin": "00:00:02",
          "ReconnectWaitMax": "00:00:10",
          "MaxReconnectRetry": -1
        }
      }
    }
  }
}
```

Auth variants (exactly one): `UserName`+`Password` | `Token` | `CredsFile` | `NKeySeedFile`.

## Publishing policies (existing schema — new publisher key)

```json
{
  "PublishingPolicies": {
    "Default": { "Publishers": [ { "Key": "nats", "Destination": "x.content.integration" } ] },
    "Rules": [
      {
        "Priority": 10,
        "Match": { "Domain": "Orders" },
        "Publishers": [
          { "Key": "rabbitmq", "Destination": "orders_exchange" },
          { "Key": "nats",     "Destination": "orders" }
        ]
      }
    ]
  }
}
```

## Delivery policies (existing schema)

Key format unchanged: `"nats:send-pending:MyDbContext"`, `"nats:retry-failed:MyDbContext"`, fallback `"default"`.

## Consumer retry policies (existing `ConsumeRetryPolicyOptions` — shared with RabbitMQ)

```json
{
  "ConsumeRetryPolicies": {
    "Default": {
      "RetryTiers": [ "10s", "1m", "5m" ],
      "IsParkingEnabled": true
    },
    "Policies": [
      {
        "Source": "orders",
        "DeadLetterDest": "parking.orders",
        "RetryTiers": [ "orders.retry.10s", "orders.retry.1m" ],
        "MaxRetryAttempts": 2,
        "IsParkingEnabled": true
      }
    ]
  }
}
```

- `Source` = consumer destination (JetStream) / original exchange (RabbitMQ).
- `RetryTiers` entries: duration (`500ms`, `10s`, `1m`, `1h`) or dotted name whose last token is a duration — so RabbitMQ tier routing keys are accepted as-is.
- `DeadLetterDest` (JetStream) = parking subject prefix; must not overlap the main stream's subjects. Omit to use `parking.{destination}`.

Invalid tier strings fail at startup with the offending value and policy `Source`.
