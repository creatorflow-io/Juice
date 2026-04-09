# Quickstart: Consumer Builder Interface

**Feature**: 001-consumer-builder-interface

---

## Before (Today)

Each transport has its own builder type with identical `Subscribe<>()` calls — no shared abstraction:

```csharp
// RabbitMQ consumer setup
services.AddRabbitMQ(...)
    .AddConsumer(consumer =>
    {
        consumer.Subscribe<OrderCreatedEvent, OrderCreatedHandler>();
        consumer.Subscribe<OrderCancelledEvent, OrderCancelledHandler>();
    });

// Local consumer setup — identical Subscribe calls, but different type
services.AddLocalConsumer(consumer =>
{
    consumer.Subscribe<OrderCreatedEvent, OrderCreatedHandler>();
    consumer.Subscribe<OrderCancelledEvent, OrderCancelledHandler>();
});

// Cannot share registration logic — consumer is a different type in each lambda
```

---

## After (This Feature)

Both builders implement `IConsumerBuilder`. Write shared registration logic once:

```csharp
// Shared extension method — works with any consumer builder
public static IConsumerBuilder SubscribeOrderEvents(this IConsumerBuilder builder)
    => builder
        .Subscribe<OrderCreatedEvent, OrderCreatedHandler>()
        .Subscribe<OrderCancelledEvent, OrderCancelledHandler>();

// RabbitMQ consumer
services.AddRabbitMQ(...)
    .AddConsumer(consumer => consumer.SubscribeOrderEvents());

// Local consumer — same extension, no duplication
services.AddLocalConsumer(consumer => consumer.SubscribeOrderEvents());
```

---

## Fluent Chaining via the Interface

Because `IConsumerBuilder.Subscribe<>()` returns `IConsumerBuilder`, full fluent chains work:

```csharp
public static IConsumerBuilder SubscribeAll(this IConsumerBuilder b)
    => b.Subscribe<EventA, HandlerA>()
       .Subscribe<EventB, HandlerB>()
       .Subscribe<EventC, HandlerC>();
```

---

## Concrete Type Still Works Unchanged

Existing code holding a concrete builder type compiles and works without modification:

```csharp
// Still compiles — concrete Subscribe returns RabbitMQConsumerBuilder
services.AddRabbitMQ(...)
    .AddConsumer(consumer =>
    {
        RabbitMQConsumerBuilder c = consumer; // concrete type
        c.Subscribe<OrderCreatedEvent, OrderCreatedHandler>()
         .ConfigureQos(10); // RabbitMQ-specific method, still accessible
    });
```
