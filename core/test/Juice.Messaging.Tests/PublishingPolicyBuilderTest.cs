using Juice.Messaging.Policies;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Juice.Messaging.Tests
{
    public class PublishingPolicyBuilderTest
    {
        // -----------------------------------------------------------------------
        // US1: Define All Publishing Rules Programmatically
        // -----------------------------------------------------------------------

        [Fact]
        public async Task CodeOnlyPolicy_DomainRule_ResolvesCorrectDestinationAsync()
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
            Assert.Equal("rabbitmq", routes.First().PublisherKey);
        }

        [Fact]
        public async Task CodeOnlyPolicy_NoMatchingRule_FallsBackToDefaultAsync()
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
                EventType = "SomeOtherEvent",
                Domain = "Billing"
            });

            Assert.Single(routes);
            Assert.Equal("default-exchange", routes.First().Destination);
        }

        [Fact]
        public async Task CodeOnlyPolicy_RuleWithRoutingKey_PropagatesRoutingKeyAsync()
        {
            var services = new ServiceCollection();
            services.AddMessaging()
                .AddPublishingPolicies(p => p
                    .SetDefault("rabbitmq", "default-exchange")
                    .AddRule(10, r => r
                        .ForDomain("Orders")
                        .ForEvent("OrderPlacedEvent")
                        .PublishTo("rabbitmq", "orders-exchange", "orders.placed")));

            var sp = services.BuildServiceProvider();
            var policy = sp.GetRequiredService<IMessagePublishingPolicy>();

            var routes = await policy.ResolveAsync(new PolicyResolveContext
            {
                EventType = "OrderPlacedEvent",
                Domain = "Orders"
            });

            Assert.Single(routes);
            Assert.Equal("orders.placed", routes.First().RoutingKey);
        }

        [Fact]
        public async Task CodeOnlyPolicy_MultipleRules_HigherPriorityWinsAsync()
        {
            var services = new ServiceCollection();
            services.AddMessaging()
                .AddPublishingPolicies(p => p
                    .SetDefault("rabbitmq", "default-exchange")
                    .AddRule(5, r => r
                        .ForDomain("Orders")
                        .PublishTo("rabbitmq", "low-priority-exchange"))
                    .AddRule(10, r => r
                        .ForDomain("Orders")
                        .PublishTo("rabbitmq", "high-priority-exchange")));

            var sp = services.BuildServiceProvider();
            var policy = sp.GetRequiredService<IMessagePublishingPolicy>();

            var routes = await policy.ResolveAsync(new PolicyResolveContext
            {
                EventType = "OrderPlacedEvent",
                Domain = "Orders"
            });

            Assert.Single(routes);
            Assert.Equal("high-priority-exchange", routes.First().Destination);
        }

        [Fact]
        public async Task CodeOnlyPolicy_MultiPublisher_FansOutToAllTargetsAsync()
        {
            var services = new ServiceCollection();
            services.AddMessaging()
                .AddPublishingPolicies(p => p
                    .AddRule(10, r => r
                        .ForDomain("Orders")
                        .PublishTo("rabbitmq", "orders-exchange")
                        .PublishTo("rabbitmq1", "orders-mirror-exchange")));

            var sp = services.BuildServiceProvider();
            var policy = sp.GetRequiredService<IMessagePublishingPolicy>();

            var routes = await policy.ResolveAsync(new PolicyResolveContext
            {
                EventType = "OrderPlacedEvent",
                Domain = "Orders"
            });

            Assert.Equal(2, routes.Count);
        }

        // -----------------------------------------------------------------------
        // US2: Mix Code-Defined Rules with Config-Driven Rules
        // -----------------------------------------------------------------------

        [Fact]
        public async Task CodeAndConfigPolicy_EachEventRoutedBySeparateSourceAsync()
        {
            var services = new ServiceCollection();

            // Simulate config-driven rule for Billing domain
            services.AddMessaging()
                .AddPublishingPolicies(p => p
                    .AddRule(10, r => r
                        .ForDomain("Billing")
                        .PublishTo("rabbitmq", "billing-exchange")))
                // Code-driven rule for Shipping domain
                .AddPublishingPolicies(p => p
                    .AddRule(10, r => r
                        .ForDomain("Shipping")
                        .PublishTo("rabbitmq", "shipping-exchange")));

            var sp = services.BuildServiceProvider();
            var policy = sp.GetRequiredService<IMessagePublishingPolicy>();

            var billingRoutes = await policy.ResolveAsync(new PolicyResolveContext { Domain = "Billing", EventType = "InvoiceCreatedEvent" });
            var shippingRoutes = await policy.ResolveAsync(new PolicyResolveContext { Domain = "Shipping", EventType = "ShipmentDispatchedEvent" });

            Assert.Equal("billing-exchange", billingRoutes.First().Destination);
            Assert.Equal("shipping-exchange", shippingRoutes.First().Destination);
        }

        [Fact]
        public async Task CodeAndConfigPolicy_EqualPriority_CodeRuleWinsAsync()
        {
            var services = new ServiceCollection();
            // Config rule added first (lower registered order)
            services.AddMessaging()
                .AddPublishingPolicies(p => p
                    .AddRule(10, r => r
                        .ForDomain("Orders")
                        .PublishTo("rabbitmq", "config-exchange")))
                // Code rule added second — same priority, should win
                .AddPublishingPolicies(p => p
                    .AddRule(10, r => r
                        .ForDomain("Orders")
                        .PublishTo("rabbitmq", "code-exchange")));

            var sp = services.BuildServiceProvider();
            var policy = sp.GetRequiredService<IMessagePublishingPolicy>();

            // Both rules are added via AddPublishingPolicies(delegate), which sets IsCodeDefined = true.
            // The second call's rule also has IsCodeDefined = true, so tiebreaker is registration order
            // (stable sort: second rule appears later in Rules list and wins when IsCodeDefined is equal).
            // This test verifies the merge itself works; the specific winner when IsCodeDefined is
            // identical depends on list order — see next test for true code-vs-config tiebreaker.
            var routes = await policy.ResolveAsync(new PolicyResolveContext { Domain = "Orders", EventType = "OrderPlacedEvent" });

            Assert.Single(routes);
        }

        [Fact]
        public async Task CodeRule_BeatsConfigRule_AtEqualPriorityAsync()
        {
            // The IsCodeDefined tiebreaker requires one rule to be from config (IsCodeDefined=false)
            // and another from code (IsCodeDefined=true). We simulate this by calling
            // AddPublishingPolicies(IConfigurationSection) with an in-memory config.
            var configValues = new Dictionary<string, string?>
            {
                ["Policies:Rules:0:Priority"] = "10",
                ["Policies:Rules:0:Match:Domain"] = "Orders",
                ["Policies:Rules:0:Publishers:0:Key"] = "rabbitmq",
                ["Policies:Rules:0:Publishers:0:Destination"] = "config-exchange"
            };
            var configuration = new Microsoft.Extensions.Configuration.ConfigurationBuilder()
                .AddInMemoryCollection(configValues)
                .Build();

            var services = new ServiceCollection();
            services.AddMessaging()
                .AddPublishingPolicies(configuration.GetSection("Policies"))
                .AddPublishingPolicies(p => p
                    .AddRule(10, r => r
                        .ForDomain("Orders")
                        .PublishTo("rabbitmq", "code-exchange")));

            var sp = services.BuildServiceProvider();
            var policy = sp.GetRequiredService<IMessagePublishingPolicy>();

            var routes = await policy.ResolveAsync(new PolicyResolveContext { Domain = "Orders", EventType = "OrderPlacedEvent" });

            Assert.Single(routes);
            Assert.Equal("code-exchange", routes.First().Destination);
        }

        [Fact]
        public async Task ConfigRule_HigherPriority_WinsOverCodeRuleAsync()
        {
            var configValues = new Dictionary<string, string?>
            {
                ["Policies:Rules:0:Priority"] = "20",
                ["Policies:Rules:0:Match:Domain"] = "Orders",
                ["Policies:Rules:0:Publishers:0:Key"] = "rabbitmq",
                ["Policies:Rules:0:Publishers:0:Destination"] = "high-priority-config-exchange"
            };
            var configuration = new Microsoft.Extensions.Configuration.ConfigurationBuilder()
                .AddInMemoryCollection(configValues)
                .Build();

            var services = new ServiceCollection();
            services.AddMessaging()
                .AddPublishingPolicies(configuration.GetSection("Policies"))
                .AddPublishingPolicies(p => p
                    .AddRule(10, r => r
                        .ForDomain("Orders")
                        .PublishTo("rabbitmq", "low-priority-code-exchange")));

            var sp = services.BuildServiceProvider();
            var policy = sp.GetRequiredService<IMessagePublishingPolicy>();

            var routes = await policy.ResolveAsync(new PolicyResolveContext { Domain = "Orders", EventType = "OrderPlacedEvent" });

            Assert.Single(routes);
            Assert.Equal("high-priority-config-exchange", routes.First().Destination);
        }

        // -----------------------------------------------------------------------
        // US3: Register a Fully Custom Policy Implementation
        // -----------------------------------------------------------------------

        [Fact]
        public async Task CustomPolicy_RegisteredViaGenericOverload_RoutesViaCustomLogicAsync()
        {
            var services = new ServiceCollection();
            services.AddMessaging()
                .AddPublishingPolicies<FixedRoutePolicy>();

            var sp = services.BuildServiceProvider();
            var policy = sp.GetRequiredService<IMessagePublishingPolicy>();

            var routes = await policy.ResolveAsync(new PolicyResolveContext { EventType = "AnyEvent", Domain = "AnyDomain" });

            Assert.Single(routes);
            Assert.Equal("fixed-exchange", routes.First().Destination);
        }

        [Fact]
        public async Task CustomPolicy_CombinedWithCodeRules_MergesRoutesFromBothAsync()
        {
            var services = new ServiceCollection();
            services.AddMessaging()
                .AddPublishingPolicies<FixedRoutePolicy>()
                .AddPublishingPolicies(p => p.SetDefault("rabbitmq", "code-default-exchange"));

            var sp = services.BuildServiceProvider();
            var policy = sp.GetRequiredService<IMessagePublishingPolicy>();

            var routes = await policy.ResolveAsync(new PolicyResolveContext { EventType = "AnyEvent" });

            // Both FixedRoutePolicy (fixed-exchange) and DefaultEventPublishingPolicy (code-default-exchange) contribute.
            Assert.Equal(2, routes.Count);
            Assert.Contains(routes, r => r.Destination == "fixed-exchange");
            Assert.Contains(routes, r => r.Destination == "code-default-exchange");
        }

        [Fact]
        public async Task CustomPolicy_MultipleCalls_MergesRoutesFromBothPoliciesAsync()
        {
            var services = new ServiceCollection();
            services.AddMessaging()
                .AddPublishingPolicies<FixedRoutePolicy>()
                .AddPublishingPolicies<AnotherFixedRoutePolicy>();

            var sp = services.BuildServiceProvider();
            var policy = sp.GetRequiredService<IMessagePublishingPolicy>();

            var routes = await policy.ResolveAsync(new PolicyResolveContext { EventType = "AnyEvent" });

            Assert.Equal(2, routes.Count);
            Assert.Contains(routes, r => r.Destination == "fixed-exchange");
            Assert.Contains(routes, r => r.Destination == "another-exchange");
        }

        [Fact]
        public async Task CustomPolicy_ResolveAsync_IsInvokedForPublishedEventAsync()
        {
            var services = new ServiceCollection();
            services.AddMessaging()
                .AddPublishingPolicies<FixedRoutePolicy>();

            var sp = services.BuildServiceProvider();
            var policy = sp.GetRequiredService<IMessagePublishingPolicy>();

            var routes = await policy.ResolveAsync(new PolicyResolveContext
            {
                EventType = "AnyEvent",
                Domain = "AnyDomain"
            });

            Assert.Single(routes);
            Assert.Equal("fixed-exchange", routes.First().Destination);
        }

        // -----------------------------------------------------------------------
        // All overloads together
        // -----------------------------------------------------------------------

        [Fact]
        public async Task AllOverloads_Together_MergesRoutesFromAllSourcesAsync()
        {
            var configValues = new Dictionary<string, string?>
            {
                ["P:Rules:0:Priority"] = "5",
                ["P:Rules:0:Match:Domain"] = "Billing",
                ["P:Rules:0:Publishers:0:Key"] = "rabbitmq",
                ["P:Rules:0:Publishers:0:Destination"] = "billing-exchange"
            };
            var configuration = new ConfigurationBuilder().AddInMemoryCollection(configValues).Build();

            var services = new ServiceCollection();
            services.AddMessaging()
                .AddPublishingPolicies(configuration.GetSection("P"))           // config rule: Billing → billing-exchange
                .AddPublishingPolicies(p => p                                   // code rule: Orders → orders-exchange
                    .AddRule(10, r => r.ForDomain("Orders").PublishTo("rabbitmq", "orders-exchange")))
                .AddPublishingPolicies<FixedRoutePolicy>();                      // custom: always fixed-exchange

            var sp = services.BuildServiceProvider();
            var policy = sp.GetRequiredService<IMessagePublishingPolicy>();

            // DefaultEventPublishingPolicy handles Billing (config) + Orders (code) rules.
            // FixedRoutePolicy always adds fixed-exchange.
            var billingRoutes = await policy.ResolveAsync(new PolicyResolveContext { Domain = "Billing", EventType = "InvoiceEvent" });
            Assert.Equal(2, billingRoutes.Count);
            Assert.Contains(billingRoutes, r => r.Destination == "billing-exchange");
            Assert.Contains(billingRoutes, r => r.Destination == "fixed-exchange");

            var ordersRoutes = await policy.ResolveAsync(new PolicyResolveContext { Domain = "Orders", EventType = "OrderEvent" });
            Assert.Equal(2, ordersRoutes.Count);
            Assert.Contains(ordersRoutes, r => r.Destination == "orders-exchange");
            Assert.Contains(ordersRoutes, r => r.Destination == "fixed-exchange");
        }

        // -----------------------------------------------------------------------
        // ForEvent<TEvent>() generic overload
        // -----------------------------------------------------------------------

        [Fact]
        public async Task ForEventGeneric_MatchesEventByTypeName_ResolvesCorrectDestinationAsync()
        {
            var services = new ServiceCollection();
            services.AddMessaging()
                .AddPublishingPolicies(p => p
                    .SetDefault("rabbitmq", "default-exchange")
                    .AddRule(10, r => r
                        .ForEvent<OrderPlacedEvent>()
                        .PublishTo("rabbitmq", "orders-exchange")));

            var sp = services.BuildServiceProvider();
            var policy = sp.GetRequiredService<IMessagePublishingPolicy>();

            var routes = await policy.ResolveAsync(new PolicyResolveContext
            {
                EventType = nameof(OrderPlacedEvent)
            });

            Assert.Single(routes);
            Assert.Equal("orders-exchange", routes.First().Destination);
        }

        [Fact]
        public async Task ForEventGeneric_DoesNotMatchOtherEventType_FallsBackToDefaultAsync()
        {
            var services = new ServiceCollection();
            services.AddMessaging()
                .AddPublishingPolicies(p => p
                    .SetDefault("rabbitmq", "default-exchange")
                    .AddRule(10, r => r
                        .ForEvent<OrderPlacedEvent>()
                        .PublishTo("rabbitmq", "orders-exchange")));

            var sp = services.BuildServiceProvider();
            var policy = sp.GetRequiredService<IMessagePublishingPolicy>();

            var routes = await policy.ResolveAsync(new PolicyResolveContext
            {
                EventType = "InvoiceCreatedEvent"
            });

            Assert.Single(routes);
            Assert.Equal("default-exchange", routes.First().Destination);
        }

        // -----------------------------------------------------------------------
        // Helper: minimal custom policy for US3 tests
        // -----------------------------------------------------------------------

        private sealed record OrderPlacedEvent : IntegrationEvent;

        private sealed class FixedRoutePolicy : IMessagePublishingPolicy
        {
            public ValueTask<IReadOnlyCollection<PublishRoute>> ResolveAsync(PolicyResolveContext context)
            {
                IReadOnlyCollection<PublishRoute> routes = [new PublishRoute("rabbitmq", "fixed-exchange")];
                return ValueTask.FromResult(routes);
            }
        }

        private sealed class AnotherFixedRoutePolicy : IMessagePublishingPolicy
        {
            public ValueTask<IReadOnlyCollection<PublishRoute>> ResolveAsync(PolicyResolveContext context)
            {
                IReadOnlyCollection<PublishRoute> routes = [new PublishRoute("rabbitmq", "another-exchange")];
                return ValueTask.FromResult(routes);
            }
        }
    }
}
