# Quickstart: Policy-Controlled Routing Key

**Feature**: `002-policy-routing-key`
**Generated**: 2026-03-03

---

## Scenario

You have a `topic` exchange in RabbitMQ and want events routed by custom routing keys (e.g., `orders.placed`, `inventory.depleted`) instead of the default event class name.

---

## Step 1: Configure routing keys in `appsettings.json`

Add a `RoutingKey` to the publisher destination inside the matching policy rule:

```json
{
  "Messaging": {
    "Publishing": {
      "Default": {
        "Publishers": [
          { "Key": "rabbitmq", "Destination": "events" }
        ]
      },
      "Rules": [
        {
          "Priority": 10,
          "Match": { "Event": "OrderPlacedEvent" },
          "Publishers": [
            {
              "Key": "rabbitmq",
              "Destination": "topic-exchange",
              "RoutingKey": "orders.placed"
            }
          ]
        },
        {
          "Priority": 10,
          "Match": { "Event": "StockDepletedEvent" },
          "Publishers": [
            {
              "Key": "rabbitmq",
              "Destination": "topic-exchange",
              "RoutingKey": "inventory.depleted"
            }
          ]
        }
      ]
    }
  }
}
```

When `OrderPlacedEvent` is published, `RabbitMQProducer` sends it to `topic-exchange` with routing key `orders.placed` — not `OrderPlacedEvent`.

---

## Step 2: Events and handlers — no changes needed

Your event and handler classes are unchanged:

```csharp
// Event (unchanged)
[Domain("orders")]
public record OrderPlacedEvent(Guid OrderId) : IntegrationEvent;

// Handler (unchanged)
public class OrderPlacedHandler : IIntegrationEventHandler<OrderPlacedEvent>
{
    public Task HandleAsync(OrderPlacedEvent @event) { /* ... */ }
}
```

---

## Step 3: Custom policy — programmatic routing key (optional)

For dynamic routing keys, implement `IMessagePublishingPolicy` directly:

```csharp
public class DomainPrefixedRoutingPolicy : IMessagePublishingPolicy
{
    public ValueTask<IReadOnlyCollection<PublishRoute>> ResolveAsync(PolicyResolveContext context)
    {
        // Build routing key as "{domain}.{eventType}" e.g. "orders.OrderPlacedEvent"
        var routingKey = context.Domain != null
            ? $"{context.Domain}.{context.EventType}"
            : context.EventType;

        IReadOnlyCollection<PublishRoute> routes =
        [
            new PublishRoute("rabbitmq", "topic-exchange", routingKey)
        ];
        return ValueTask.FromResult(routes);
    }
}
```

Register it in DI:

```csharp
services.AddSingleton<IMessagePublishingPolicy, DomainPrefixedRoutingPolicy>();
```

---

## Behavior when `RoutingKey` is omitted

Existing configurations without a `RoutingKey` field are **unchanged**. `RabbitMQProducer` falls through to the existing chain:

```
x-routing-key (absent) → x-message-name ("OrderPlacedEvent") → used as routing key
```

---

## Validation checklist

- [ ] `dotnet build Juice.sln` passes with zero errors
- [ ] Publish an `OrderPlacedEvent` and confirm the message arrives in the queue bound to `orders.placed`, not `OrderPlacedEvent`
- [ ] Confirm existing events with no policy `RoutingKey` configured still route correctly by event name
- [ ] Run `dotnet test core/test/Juice.EventBus.Tests/` and confirm `PublishPoliciesTest` tests pass including new routing-key tests
