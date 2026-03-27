# Contract: DeliveryProcessorBuilder API

**Branch**: `009-processor-delivery-policy` | **Date**: 2026-03-27

## New Public Methods on `DeliveryProcessorBuilder`

`DeliveryProcessorBuilder` is the fluent builder received in the `configure` callback of `AddDeliveryProcessor<TContext>()`.

---

### `AddDeliveryPolicies(IConfigurationSection)`

```
DeliveryProcessorBuilder AddDeliveryPolicies(IConfigurationSection policies)
```

Configures delivery policies for this processor from a configuration section. The section is bound to `DeliveryPolicyOptions` (same shape as the global `AddDeliveryPolicies` on `DeliveryBuilder`).

**Parameters**:
- `policies` — configuration section whose keys map to `DeliveryPolicyOptions` fields

**Returns**: `this` (fluent)

**Behavior**: Safe to call multiple times — each call's settings are merged (later calls win for overlapping fields).

**appsettings example** — configure just the default policy for this processor:
```json
{
  "MyProcessorPolicy": {
    "DefaultPolicy": {
      "BatchSize": 50,
      "Interval": "00:00:03"
    }
  }
}
```
```csharp
builder.AddDeliveryProcessor<OrderDbContext>("rabbitmq", proc =>
    proc.AddDeliveryPolicies(config.GetSection("MyProcessorPolicy")));
```

---

### `AddDeliveryPolicies(Action<DeliveryPolicyOptions>)`

```
DeliveryProcessorBuilder AddDeliveryPolicies(Action<DeliveryPolicyOptions> configure)
```

Configures delivery policies for this processor via a delegate.

**Parameters**:
- `configure` — delegate that receives the `DeliveryPolicyOptions` instance for this processor

**Returns**: `this` (fluent)

**Behavior**: Safe to call multiple times — each delegate is applied in order.

**Examples**:

```csharp
// Configure only the default policy (common case)
builder.AddDeliveryProcessor<OrderDbContext>("rabbitmq", proc =>
    proc.AddDeliveryPolicies(opts => {
        opts.DefaultPolicy = new PolicyConfiguration {
            BatchSize = 50,
            Interval = TimeSpan.FromSeconds(3)
        };
    }));
```

```csharp
// Override only one field — all others inherit from global/built-in defaults
builder.AddDeliveryProcessor<AuditDbContext>("local", proc =>
    proc.AddDeliveryPolicies(opts =>
        opts.DefaultPolicy = new PolicyConfiguration { BatchSize = 5 }));
```

---

## Existing API — Unchanged

The following existing methods on `DeliveryBuilder` and `DeliveryProcessorBuilder` are **not changed**:

| Method | Location | Status |
|--------|----------|--------|
| `AddDeliveryProcessor<T>(publisher, Action<DeliveryProcessorBuilder>?)` | `DeliveryBuilder` | Unchanged signature |
| `AddDeliveryProcessor<T>(publisher, params string[])` | `DeliveryBuilder` | Unchanged signature |
| `AddDeliveryPolicies(IConfigurationSection)` | `DeliveryBuilder` | Unchanged |
| `AddDeliveryPolicies(Action<DeliveryPolicyOptions>)` | `DeliveryBuilder` | Unchanged |
| `WithIntents(params string[])` | `DeliveryProcessorBuilder` | Unchanged |
| `WithDefaultIntents()` | `DeliveryProcessorBuilder` | Unchanged |
| `ClearIntents()` | `DeliveryProcessorBuilder` | Unchanged |

---

## Policy Resolution Contract

When the delivery worker for processor `(publisher="rabbitmq", TContext=OrderDbContext)` resolves its policy:

1. **Processor-scoped lookup** (new): checks named `DeliveryPolicyOptions` under key `"rabbitmq:OrderDbContext"` — both its `Policies` dictionary and its `DefaultPolicy`.
2. **Global lookup** (existing): wildcard matching in the unnamed `DeliveryPolicyOptions` followed by global `DefaultPolicy`.
3. **Built-in default** (existing): `DeliveryPolicy.Default` hardcoded values.

At step 1, if a processor `DefaultPolicy` is found, it is merged on top of the result from step 2 using field-level null-coalescing: only fields explicitly set in the processor policy override the global value; null fields inherit from the globally resolved policy.

**Guarantee**: Processors with no `AddDeliveryPolicies` call behave identically to before this feature (zero behavioral change).
