# Public API Contracts: In-Code Publishing Policies

## New Overloads on MessagingBuilder

### 1. Code-Defined Rules Overload

```csharp
// Namespace: Juice.Messaging
public sealed class MessagingBuilder
{
    /// <summary>
    /// Configures publishing policy rules in code. Rules defined here are merged with
    /// any rules registered via <see cref="AddPublishingPolicies(IConfigurationSection)"/>.
    /// When a code-defined rule and a config rule share the same priority, the code
    /// rule takes precedence.
    /// </summary>
    /// <param name="configure">Delegate that receives a <see cref="PublishingPolicyBuilder"/>
    /// to define the default route and any match rules.</param>
    public MessagingBuilder AddPublishingPolicies(Action<PublishingPolicyBuilder> configure);
}
```

**Behavior**:
- Registers `DefaultEventPublishingPolicy` as `IMessagePublishingPolicy` (singleton, guarded — skips if already registered).
- Applies `configure` to a new `PublishingPolicyBuilder` and registers the resulting rules via `services.Configure<PublishingPolicyOptions>`.
- Safe to call alongside `AddPublishingPolicies(IConfigurationSection)` — both calls take effect.

---

### 2. Custom Policy Overload

```csharp
// Namespace: Juice.Messaging
public sealed class MessagingBuilder
{
    /// <summary>
    /// Registers a custom <see cref="IMessagePublishingPolicy"/> implementation.
    /// When this overload is used, <see cref="DefaultEventPublishingPolicy"/> is NOT
    /// registered. If <see cref="AddPublishingPolicies(IConfigurationSection)"/> or
    /// <see cref="AddPublishingPolicies(Action{PublishingPolicyBuilder})"/> was called
    /// first, the custom registration is a no-op (first-registration wins).
    /// </summary>
    public MessagingBuilder AddPublishingPolicies<TPolicy>()
        where TPolicy : class, IMessagePublishingPolicy;
}
```

**Behavior**:
- Calls `services.TryAddSingleton<IMessagePublishingPolicy, TPolicy>()`.
- Does NOT register `PublishingPolicyOptions` — `TPolicy` manages its own configuration.

---

## New Public Type: PublishingPolicyBuilder

```csharp
// Namespace: Juice.Messaging.Policies
public sealed class PublishingPolicyBuilder
{
    /// <summary>
    /// Sets the default publisher route used when no rule matches a published event.
    /// Replaces any previously set default within this builder instance.
    /// </summary>
    public PublishingPolicyBuilder SetDefault(
        string publisherKey,
        string destination,
        string? routingKey = null);

    /// <summary>
    /// Appends a routing rule. Rules are evaluated in descending priority order;
    /// the first matching rule wins. At equal priority, code-defined rules take
    /// precedence over config-defined rules.
    /// </summary>
    public PublishingPolicyBuilder AddRule(
        int priority,
        Action<PublishRuleBuilder> configure);
}
```

---

## New Public Type: PublishRuleBuilder

```csharp
// Namespace: Juice.Messaging.Policies
public sealed class PublishRuleBuilder
{
    /// <summary>Constrains the rule to events whose type name matches <paramref name="eventTypeName"/>
    /// (case-insensitive). If not called, the rule matches events of any type.</summary>
    public PublishRuleBuilder ForEvent(string eventTypeName);

    /// <summary>Constrains the rule to events decorated with <c>[Domain(<paramref name="domain"/>)]</c>
    /// (case-insensitive). If not called, the rule matches events of any domain.</summary>
    public PublishRuleBuilder ForDomain(string domain);

    /// <summary>Constrains the rule to events published under a specific tenant identifier
    /// (case-insensitive). If not called, the rule matches all tenants.</summary>
    public PublishRuleBuilder ForTenant(string tenantIdentifier);

    /// <summary>Constrains the rule to events published under a specific tenant tier
    /// (case-insensitive). If not called, the rule matches all tenant tiers.</summary>
    public PublishRuleBuilder ForTenantTier(string tenantTier);

    /// <summary>
    /// Adds a publisher target for this rule. Call multiple times to fan out to
    /// multiple publishers.
    /// </summary>
    public PublishRuleBuilder PublishTo(
        string publisherKey,
        string destination,
        string? routingKey = null);
}
```

---

## Usage Examples

### All rules in code (no JSON config)

```csharp
services.AddMessaging()
    .AddPublishingPolicies(policy => policy
        .SetDefault("rabbitmq", "events-exchange")
        .AddRule(10, rule => rule
            .ForDomain("Orders")
            .ForEvent("OrderPlacedEvent")
            .PublishTo("rabbitmq", "orders-exchange", "orders.placed"))
        .AddRule(5, rule => rule
            .ForDomain("Billing")
            .PublishTo("rabbitmq", "billing-exchange")));
```

### Code rules supplementing JSON config

```csharp
// appsettings.json provides the default + common rules
services.AddMessaging()
    .AddPublishingPolicies(configuration.GetSection("PublishingPolicies"))
    // additional module-level overrides registered in code:
    .AddPublishingPolicies(policy => policy
        .AddRule(20, rule => rule
            .ForDomain("Shipping")
            .PublishTo("rabbitmq", "shipping-exchange", "shipping.events")));
```

### Custom policy implementation

```csharp
services.AddMessaging()
    .AddPublishingPolicies<MyDynamicPublishingPolicy>();
```

---

## Compatibility

- All existing calls to `AddPublishingPolicies(IConfigurationSection)` continue to work without modification.
- `IMessagePublishingPolicy`, `PublishRoute`, `PolicyResolveContext` are unchanged.
- `PublishingPolicyOptions`, `PublishRule`, `PublishRuleMatch`, `PublisherDestination` remain `internal` — not exposed.
