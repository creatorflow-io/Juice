# Quickstart: Per-Processor Delivery Policy

**Branch**: `009-processor-delivery-policy` | **Date**: 2026-03-27

---

## Scenario A — Per-processor policy via code

Give a high-throughput RabbitMQ processor a larger batch and faster poll interval, while a background audit processor runs slower.

```csharp
services.AddMessaging()
    .AddDelivery(delivery =>
    {
        // High-throughput order processor
        delivery.AddDeliveryProcessor<OrderDbContext>("rabbitmq", proc =>
            proc.AddDeliveryPolicies(opts =>
                opts.DefaultPolicy = new PolicyConfiguration
                {
                    BatchSize = 100,
                    Interval = TimeSpan.FromSeconds(1)
                }));

        // Low-priority audit processor — inherits global defaults for everything else
        delivery.AddDeliveryProcessor<AuditDbContext>("rabbitmq", proc =>
            proc.AddDeliveryPolicies(opts =>
                opts.DefaultPolicy = new PolicyConfiguration
                {
                    BatchSize = 5,
                    Interval = TimeSpan.FromSeconds(30)
                }));
    });
```

---

## Scenario B — Per-processor policy via appsettings

`appsettings.json`:
```json
{
  "Delivery": {
    "Order": {
      "DefaultPolicy": {
        "BatchSize": 100,
        "Interval": "00:00:01"
      }
    },
    "Audit": {
      "DefaultPolicy": {
        "BatchSize": 5,
        "Interval": "00:00:30"
      }
    }
  }
}
```

```csharp
services.AddMessaging()
    .AddDelivery(delivery =>
    {
        delivery.AddDeliveryProcessor<OrderDbContext>("rabbitmq", proc =>
            proc.AddDeliveryPolicies(config.GetSection("Delivery:Order")));

        delivery.AddDeliveryProcessor<AuditDbContext>("rabbitmq", proc =>
            proc.AddDeliveryPolicies(config.GetSection("Delivery:Audit")));
    });
```

---

## Scenario C — Global fallback (no processor policy)

Processors without explicit policies use global delivery policies unchanged.

```csharp
services.AddMessaging()
    .AddDelivery(delivery =>
    {
        // Global policy applies to all processors without their own policy
        delivery.AddDeliveryPolicies(opts =>
            opts.DefaultPolicy = new PolicyConfiguration { BatchSize = 20 });

        // This processor has no explicit policy — inherits BatchSize = 20 from global
        delivery.AddDeliveryProcessor<ReportDbContext>("rabbitmq");
    });
```

---

## Scenario D — Partial override

Override only retry settings for one processor; all other parameters come from the global policy.

```csharp
services.AddMessaging()
    .AddDelivery(delivery =>
    {
        // Global: BatchSize = 20, Interval = 5s, MaxRetryAttempts = 3
        delivery.AddDeliveryPolicies(opts =>
            opts.DefaultPolicy = new PolicyConfiguration
            {
                BatchSize = 20,
                Interval = TimeSpan.FromSeconds(5),
                MaxRetryAttempts = 3
            });

        // This processor overrides only MaxRetryAttempts; BatchSize and Interval come from global
        delivery.AddDeliveryProcessor<CriticalDbContext>("rabbitmq", proc =>
            proc.AddDeliveryPolicies(opts =>
                opts.DefaultPolicy = new PolicyConfiguration
                {
                    MaxRetryAttempts = 10
                }));
    });
```

Result for `CriticalDbContext`: `BatchSize = 20`, `Interval = 5s`, `MaxRetryAttempts = 10`.
