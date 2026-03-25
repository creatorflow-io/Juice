# Quickstart: In-Code Publishing Policies

## Scenario A — Define All Policies in Code

Replace a JSON-based publishing policy with a fully code-defined equivalent.

**Before** (JSON-only):
```json
// appsettings.json
"PublishingPolicies": {
  "Default": {
    "Publishers": [{ "Key": "rabbitmq", "Destination": "events-exchange" }]
  },
  "Rules": [
    {
      "Priority": 10,
      "Match": { "Domain": "Orders", "Event": "OrderPlacedEvent" },
      "Publishers": [{ "Key": "rabbitmq", "Destination": "orders-exchange", "RoutingKey": "orders.placed" }]
    }
  ]
}
```
```csharp
services.AddMessaging()
    .AddPublishingPolicies(configuration.GetSection("PublishingPolicies"));
```

**After** (code-only):
```csharp
services.AddMessaging()
    .AddPublishingPolicies(policy => policy
        .SetDefault("rabbitmq", "events-exchange")
        .AddRule(10, rule => rule
            .ForDomain("Orders")
            .ForEvent("OrderPlacedEvent")
            .PublishTo("rabbitmq", "orders-exchange", "orders.placed")));
```

No `appsettings.json` `PublishingPolicies` section needed.

---

## Scenario B — Add Module Rules Alongside Existing Config

A shared library or module adds its own routing rules without touching the host's `appsettings.json`:

```csharp
// Host startup (existing code — unchanged)
services.AddMessaging()
    .AddPublishingPolicies(configuration.GetSection("PublishingPolicies"));

// Module registration (new code — additive)
services.AddMessaging()
    .AddPublishingPolicies(policy => policy
        .AddRule(20, rule => rule
            .ForDomain("Shipping")
            .PublishTo("rabbitmq", "shipping-exchange", "shipping.#")));
```

Both sets of rules are active. The module rule (priority 20) beats any config rule at the same or lower priority.

---

## Scenario C — Custom Policy Implementation

When routing logic cannot be expressed as simple match rules:

```csharp
// 1. Implement the interface
public class TenantDrivenPolicy : IMessagePublishingPolicy
{
    private readonly ITenantPolicyStore _store;
    public TenantDrivenPolicy(ITenantPolicyStore store) => _store = store;

    public async ValueTask<IReadOnlyCollection<PublishRoute>> ResolveAsync(PolicyResolveContext context)
    {
        var route = await _store.GetRouteAsync(context.TenantIdentifier, context.Domain);
        return route is null
            ? [new PublishRoute("rabbitmq", "default-exchange")]
            : [new PublishRoute(route.PublisherKey, route.Destination)];
    }
}

// 2. Register it
services.AddMessaging()
    .AddPublishingPolicies<TenantDrivenPolicy>();
```

The custom policy is the sole active policy — `DefaultEventPublishingPolicy` is not registered.

---

## Priority Reference

| Priority | Source | Wins over |
|----------|--------|-----------|
| Higher number | Code or config | Any rule with lower priority |
| Equal priority | Code-defined | Config-defined rule at same priority |
| Equal priority + same source | First in registration order | Later-registered rule |
| No rule matches | Default (SetDefault / JSON Default) | — |

---

## Testing Without Infrastructure

Policy resolution can be unit-tested with no RabbitMQ or database dependency:

```csharp
[Fact]
public async Task CodeRule_OverridesDefault_WhenDomainMatchesAsync()
{
    var services = new ServiceCollection();
    services.AddMessaging()
        .AddPublishingPolicies(p => p
            .SetDefault("rabbitmq", "default-exchange")
            .AddRule(10, r => r
                .ForDomain("Orders")
                .PublishTo("rabbitmq", "orders-exchange")));

    var sp = services.BuildServiceProvider();
    var policy = sp.GetRequiredService<IMessagePublishingPolicy>();

    var routes = await policy.ResolveAsync(new PolicyResolveContext
    {
        EventType = "OrderPlacedEvent",
        Domain = "Orders"
    });

    Assert.Single(routes);
    Assert.Equal("orders-exchange", routes.First().Destination);
}
```
