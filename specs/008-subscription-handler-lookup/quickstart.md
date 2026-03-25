# Quickstart: Subscriptions Manager Handler Lookup for Local Routes

**Feature**: 008-subscription-handler-lookup

---

## Before (existing behavior)

Handlers registered in DI are discovered automatically at dispatch time via a container scan. No explicit subscription registration is required.

```csharp
// Program.cs — existing approach
services.AddMessaging(messaging =>
{
    messaging.AddLocalChannel();
    messaging.AddLocalPublisher();
});

// Handler registration — implicit discovery
services.AddTransient<IIntegrationEventHandler<OrderCreatedEvent>, OrderCreatedHandler>();
```

With the existing approach, **every** `IIntegrationEventHandler<OrderCreatedEvent>` in the DI container is invoked when an `OrderCreatedEvent` arrives — even handlers added by other library packages, test doubles, etc.

---

## After (with explicit subscription registry)

Use `AddLocalConsumer` to register handlers explicitly. The subscriptions manager then controls exactly which handlers are invoked.

```csharp
// Program.cs — new explicit approach
services.AddMessaging(messaging =>
{
    messaging.AddLocalChannel();     // "local-channel" route
    messaging.AddLocalPublisher();   // "local" outbox-backed route

    // Register handlers for local routes via the subscriptions manager
    messaging.AddLocalConsumer(consumer =>
    {
        consumer.Subscribe<OrderCreatedEvent, OrderCreatedHandler>();
        consumer.Subscribe<OrderShippedEvent, OrderShippedHandler>();
    });
});
```

This registers:
1. `OrderCreatedHandler` and `OrderShippedHandler` as transient DI services.
2. A keyed `ISubscriptionsManager` (key `"local"`) containing the two subscriptions.

When `OrderCreatedEvent` arrives on `"local-channel"` or `"local"`, only `OrderCreatedHandler` is invoked — exactly as registered.

---

## Multiple handler registrations for the same event

```csharp
messaging.AddLocalConsumer(consumer =>
{
    consumer.Subscribe<OrderCreatedEvent, EmailNotificationHandler>();
    consumer.Subscribe<OrderCreatedEvent, AuditLogHandler>();
});
```

Both handlers are invoked in registration order when `OrderCreatedEvent` is dispatched.

---

## Custom routing key

Use a routing key override when the event name alone is not a sufficient discriminator.

```csharp
messaging.AddLocalConsumer(consumer =>
{
    consumer.Subscribe<OrderCreatedEvent, OrderCreatedHandler>(key: "orders.created");
});
```

The dispatcher will look up handlers by the key `"orders.created"` instead of the default `"OrderCreatedEvent"`.

---

## Querying the registry (introspection)

```csharp
// Inject ISubscriptionsManager for the "local" key
public class DiagnosticsService(
    [FromKeyedServices("local")] ISubscriptionsManager subscriptionsManager)
{
    public async Task<IEnumerable<Type>> GetHandlersAsync(string eventName)
        => await subscriptionsManager.GetHandlersForEventAsync(eventName);
}
```

---

## Backward compatibility

If `AddLocalConsumer` is never called, local-channel and local routes continue to discover handlers via the DI container scan (existing behavior). No changes to existing code are required to maintain the pre-feature behavior.

---

## Testing

```csharp
[Collection("LocalChannel")]
[InitializeMessageContext]
public class OrderCreatedDispatchTests
{
    [Fact]
    public async Task Should_invoke_registered_handler_via_subscriptions_manager_Async()
    {
        // Arrange: build host with AddLocalConsumer
        var host = Host.CreateDefaultBuilder()
            .ConfigureServices(services =>
            {
                services.AddMessaging(m =>
                {
                    m.AddLocalChannel();
                    m.AddLocalConsumer(c =>
                        c.Subscribe<OrderCreatedEvent, OrderCreatedHandler>());
                });
            })
            .Build();

        // Act: publish event on local-channel
        var publisher = host.Services.GetRequiredKeyedService<IMessagePublisher>("local-channel");
        await publisher.PublishAsync(new OrderCreatedEvent { OrderId = "42" });

        // Assert: handler invocation verified via test double / spy
        // ...
    }
}
```
