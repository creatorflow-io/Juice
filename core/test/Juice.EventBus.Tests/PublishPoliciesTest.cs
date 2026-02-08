using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Juice.Messaging.Policies.Internal;
using Microsoft.Extensions.Options;
using Xunit;
using Juice.Messaging.Policies;

namespace Juice.EventBus.Tests
{
    public class PublishPoliciesTest
    {
        private static PolicyResolveContext CreateDefaultContext() =>
        new()
        {
            EventType = "ContentPublishedIntegrationEvent",
            Domain = "content",
            TenantIdentifier = "tenant-a",
            TenantTier = "enterprise"
        };

        [Fact(DisplayName = "Should use default when no rules match")]
        public async Task Should_Use_Default_When_No_Rules_MatchAsync()
        {
            // Arrange
            var options = new PublishingPolicyOptions
            {
                Default = new PublishRule
                {
                    Publishers =
                    [
                        new PublisherDestination { Key = "rabbitmq", Destination = "default_exchange" }
                    ]
                },
                Rules = []
            };
            var policy = new DefaultEventPublishingPolicy(Options.Create(options));
            var context = CreateDefaultContext();

            // Act
            var routes = await policy.ResolveAsync(context);

            // Assert
            routes.Should().NotBeNull();
            routes.Should().HaveCount(1);
            routes.First().PublisherKey.Should().Be("rabbitmq");
            routes.First().Destination.Should().Be("default_exchange");
        }

        [Fact(DisplayName = "Should match rule by event type only")]
        public async Task Should_Match_Rule_By_Event_Type_OnlyAsync()
        {
            // Arrange
            var options = new PublishingPolicyOptions
            {
                Default = new PublishRule
                {
                    Publishers = [new PublisherDestination { Key = "rabbitmq", Destination = "default" }]
                },
                Rules =
                [
                    new PublishRule
                    {
                        Priority = 10,
                        Match = new PublishRuleMatch { Event = "ContentPublishedIntegrationEvent" },
                        Publishers = [new PublisherDestination { Key = "rabbitmq", Destination = "x.content.integration" }]
                    }
                ]
            };
            var policy = new DefaultEventPublishingPolicy(Options.Create(options));
            var context = CreateDefaultContext();

            // Act
            var routes = await policy.ResolveAsync(context);

            // Assert
            routes.Should().HaveCount(1);
            routes.First().Destination.Should().Be("x.content.integration");
        }

        [Fact(DisplayName = "Should match rule by tenant tier only")]
        public async Task Should_Match_Rule_By_Tenant_Tier_OnlyAsync()
        {
            // Arrange
            var options = new PublishingPolicyOptions
            {
                Default = new PublishRule
                {
                    Publishers = [new PublisherDestination { Key = "rabbitmq", Destination = "default" }]
                },
                Rules =
                [
                    new PublishRule
                    {
                        Priority = 10,
                        Match = new PublishRuleMatch { TenantTier = "enterprise" },
                        Publishers = [new PublisherDestination { Key = "kafka", Destination = "x.premium.stream" }]
                    }
                ]
            };
            var policy = new DefaultEventPublishingPolicy(Options.Create(options));
            var context = CreateDefaultContext();

            // Act
            var routes = await policy.ResolveAsync(context);

            // Assert
            routes.Should().HaveCount(1);
            routes.First().PublisherKey.Should().Be("kafka");
            routes.First().Destination.Should().Be("x.premium.stream");
        }

        [Fact(DisplayName = "Should match rule by event and tenant tier combination")]
        public async Task Should_Match_Rule_By_Event_And_TenantTierAsync()
        {
            // Arrange
            var options = new PublishingPolicyOptions
            {
                Default = new PublishRule
                {
                    Publishers = [new PublisherDestination { Key = "rabbitmq", Destination = "default" }]
                },
                Rules =
                [
                    new PublishRule
                    {
                        Priority = 10,
                        Match = new PublishRuleMatch { Event = "ContentPublishedIntegrationEvent" },
                        Publishers = [new PublisherDestination { Key = "rabbitmq", Destination = "x.content.integration" }]
                    },
                    new PublishRule
                    {
                        Priority = 20,
                        Match = new PublishRuleMatch
                        {
                            Event = "ContentPublishedIntegrationEvent",
                            TenantTier = "enterprise"
                        },
                        Publishers = [new PublisherDestination { Key = "rabbitmq", Destination = "x.content.integration.vip" }]
                    }
                ]
            };
            var policy = new DefaultEventPublishingPolicy(Options.Create(options));
            var context = CreateDefaultContext();

            // Act
            var routes = await policy.ResolveAsync(context);

            // Assert
            routes.Should().HaveCount(1);
            routes.First().Destination.Should().Be("x.content.integration.vip");
        }

        [Fact(DisplayName = "Should prioritize higher priority rule")]
        public async Task Should_Prioritize_Higher_Priority_RuleAsync()
        {
            // Arrange
            var options = new PublishingPolicyOptions
            {
                Default = new PublishRule
                {
                    Publishers = [new PublisherDestination { Key = "rabbitmq", Destination = "default" }]
                },
                Rules =
                [
                    new PublishRule
                    {
                        Priority = 5,
                        Match = new PublishRuleMatch { Event = "ContentPublishedIntegrationEvent" },
                        Publishers = [new PublisherDestination { Key = "rabbitmq", Destination = "low_priority" }]
                    },
                    new PublishRule
                    {
                        Priority = 50,
                        Match = new PublishRuleMatch { Event = "ContentPublishedIntegrationEvent" },
                        Publishers = [new PublisherDestination { Key = "rabbitmq", Destination = "high_priority" }]
                    }
                ]
            };
            var policy = new DefaultEventPublishingPolicy(Options.Create(options));
            var context = CreateDefaultContext();

            // Act
            var routes = await policy.ResolveAsync(context);

            // Assert
            routes.Should().HaveCount(1);
            routes.First().Destination.Should().Be("high_priority");
        }

        [Fact(DisplayName = "Should match rule by specific tenant ID")]
        public async Task Should_Match_Rule_By_Specific_TenantIdAsync()
        {
            // Arrange
            var options = new PublishingPolicyOptions
            {
                Default = new PublishRule
                {
                    Publishers = [new PublisherDestination { Key = "rabbitmq", Destination = "default" }]
                },
                Rules =
                [
                    new PublishRule
                    {
                        Priority = 100,
                        Match = new PublishRuleMatch
                        {
                            Event = "ContentPublishedIntegrationEvent",
                            TenantIdentifier = "tenant-a"
                        },
                        Publishers = [new PublisherDestination { Key = "rabbitmq", Destination = "x.tenant.a.vip" }]
                    }
                ]
            };
            var policy = new DefaultEventPublishingPolicy(Options.Create(options));
            var context = CreateDefaultContext();

            // Act
            var routes = await policy.ResolveAsync(context);

            // Assert
            routes.Should().HaveCount(1);
            routes.First().Destination.Should().Be("x.tenant.a.vip");
        }

        [Fact(DisplayName = "Should match rule by domain")]
        public async Task Should_Match_Rule_By_DomainAsync()
        {
            // Arrange
            var options = new PublishingPolicyOptions
            {
                Default = new PublishRule
                {
                    Publishers = [new PublisherDestination { Key = "rabbitmq", Destination = "default" }]
                },
                Rules =
                [
                    new PublishRule
                    {
                        Priority = 15,
                        Match = new PublishRuleMatch { Domain = "content" },
                        Publishers = [new PublisherDestination { Key = "rabbitmq", Destination = "x.content.domain" }]
                    }
                ]
            };
            var policy = new DefaultEventPublishingPolicy(Options.Create(options));
            var context = CreateDefaultContext();

            // Act
            var routes = await policy.ResolveAsync(context);

            // Assert
            routes.Should().HaveCount(1);
            routes.First().Destination.Should().Be("x.content.domain");
        }

        [Fact(DisplayName = "Should match all criteria (highest specificity)")]
        public async Task Should_Match_All_CriteriaAsync()
        {
            // Arrange
            var options = new PublishingPolicyOptions
            {
                Default = new PublishRule
                {
                    Publishers = [new PublisherDestination { Key = "rabbitmq", Destination = "default" }]
                },
                Rules =
                [
                    new PublishRule
                    {
                        Priority = 10,
                        Match = new PublishRuleMatch { Event = "ContentPublishedIntegrationEvent" },
                        Publishers = [new PublisherDestination { Key = "rabbitmq", Destination = "event_only" }]
                    },
                    new PublishRule
                    {
                        Priority = 100,
                        Match = new PublishRuleMatch
                        {
                            Event = "ContentPublishedIntegrationEvent",
                            Domain = "content",
                            TenantIdentifier = "tenant-a",
                            TenantTier = "enterprise"
                        },
                        Publishers = [new PublisherDestination { Key = "rabbitmq", Destination = "x.all.match" }]
                    }
                ]
            };
            var policy = new DefaultEventPublishingPolicy(Options.Create(options));
            var context = CreateDefaultContext();

            // Act
            var routes = await policy.ResolveAsync(context);

            // Assert
            routes.Should().HaveCount(1);
            routes.First().Destination.Should().Be("x.all.match");
        }

        [Fact(DisplayName = "Should not match when tenant tier differs")]
        public async Task Should_Not_Match_When_TenantTier_DiffersAsync()
        {
            // Arrange
            var options = new PublishingPolicyOptions
            {
                Default = new PublishRule
                {
                    Publishers = [new PublisherDestination { Key = "rabbitmq", Destination = "default" }]
                },
                Rules =
                [
                    new PublishRule
                    {
                        Priority = 10,
                        Match = new PublishRuleMatch
                        {
                            Event = "ContentPublishedIntegrationEvent",
                            TenantTier = "free"
                        },
                        Publishers = [new PublisherDestination { Key = "rabbitmq", Destination = "x.free.tier" }]
                    }
                ]
            };
            var policy = new DefaultEventPublishingPolicy(Options.Create(options));
            var context = CreateDefaultContext(); // Has tier = "enterprise"

            // Act
            var routes = await policy.ResolveAsync(context);

            // Assert - Should use default since tenant tier doesn't match
            routes.Should().HaveCount(1);
            routes.First().Destination.Should().Be("default");
        }

        [Fact(DisplayName = "Should return multiple publishers for same rule")]
        public async Task Should_Return_Multiple_Publishers_For_Same_RuleAsync()
        {
            // Arrange
            var options = new PublishingPolicyOptions
            {
                Default = new PublishRule
                {
                    Publishers = [new PublisherDestination { Key = "rabbitmq", Destination = "default" }]
                },
                Rules =
                [
                    new PublishRule
                    {
                        Priority = 10,
                        Match = new PublishRuleMatch { Event = "ContentPublishedIntegrationEvent" },
                        Publishers =
                        [
                            new PublisherDestination { Key = "rabbitmq", Destination = "x.content.integration" },
                            new PublisherDestination { Key = "kafka", Destination = "content.topic" },
                            new PublisherDestination { Key = "servicebus", Destination = "content-queue" }
                        ]
                    }
                ]
            };
            var policy = new DefaultEventPublishingPolicy(Options.Create(options));
            var context = CreateDefaultContext();

            // Act
            var routes = await policy.ResolveAsync(context);

            // Assert
            routes.Should().HaveCount(3);
            routes.Should().Contain(r => r.PublisherKey == "rabbitmq" && r.Destination == "x.content.integration");
            routes.Should().Contain(r => r.PublisherKey == "kafka" && r.Destination == "content.topic");
            routes.Should().Contain(r => r.PublisherKey == "servicebus" && r.Destination == "content-queue");
        }

        [Fact(DisplayName = "Should be case insensitive for matching")]
        public async Task Should_Be_Case_Insensitive_For_MatchingAsync()
        {
            // Arrange
            var options = new PublishingPolicyOptions
            {
                Default = new PublishRule
                {
                    Publishers = [new PublisherDestination { Key = "rabbitmq", Destination = "default" }]
                },
                Rules =
                [
                    new PublishRule
                    {
                        Priority = 10,
                        Match = new PublishRuleMatch
                        {
                            Event = "contentpublishedintegrationevent",
                            TenantTier = "ENTERPRISE"
                        },
                        Publishers = [new PublisherDestination { Key = "rabbitmq", Destination = "x.matched" }]
                    }
                ]
            };
            var policy = new DefaultEventPublishingPolicy(Options.Create(options));
            var context = CreateDefaultContext();

            // Act
            var routes = await policy.ResolveAsync(context);

            // Assert
            routes.Should().HaveCount(1);
            routes.First().Destination.Should().Be("x.matched");
        }

        [Fact(DisplayName = "Should handle null tenant context")]
        public async Task Should_Handle_Null_Tenant_ContextAsync()
        {
            // Arrange
            var options = new PublishingPolicyOptions
            {
                Default = new PublishRule
                {
                    Publishers = [new PublisherDestination { Key = "rabbitmq", Destination = "default" }]
                },
                Rules =
                [
                    new PublishRule
                    {
                        Priority = 10,
                        Match = new PublishRuleMatch { Event = "ContentPublishedIntegrationEvent" },
                        Publishers = [new PublisherDestination { Key = "rabbitmq", Destination = "x.content" }]
                    }
                ]
            };
            var policy = new DefaultEventPublishingPolicy(Options.Create(options));
            var context = new PolicyResolveContext
            {
                EventType = "ContentPublishedIntegrationEvent",
                Domain = "content",
                TenantIdentifier = null,
                TenantTier = null
            };

            // Act
            var routes = await policy.ResolveAsync(context);

            // Assert
            routes.Should().HaveCount(1);
            routes.First().Destination.Should().Be("x.content");
        }

        [Fact(DisplayName = "Should not match rule requiring tenant when tenant is null")]
        public async Task Should_Not_Match_Rule_Requiring_Tenant_When_Tenant_Is_NullAsync()
        {
            // Arrange
            var options = new PublishingPolicyOptions
            {
                Default = new PublishRule
                {
                    Publishers = [new PublisherDestination { Key = "rabbitmq", Destination = "default" }]
                },
                Rules =
                [
                    new PublishRule
                    {
                        Priority = 10,
                        Match = new PublishRuleMatch
                        {
                            Event = "ContentPublishedIntegrationEvent",
                            TenantTier = "enterprise"
                        },
                        Publishers = [new PublisherDestination { Key = "rabbitmq", Destination = "x.enterprise" }]
                    }
                ]
            };
            var policy = new DefaultEventPublishingPolicy(Options.Create(options));
            var context = new PolicyResolveContext
            {
                EventType = "ContentPublishedIntegrationEvent",
                Domain = "content",
                TenantIdentifier = null,
                TenantTier = null
            };

            // Act
            var routes = await policy.ResolveAsync(context);

            // Assert - Should use default since tenant tier is required but null
            routes.Should().HaveCount(1);
            routes.First().Destination.Should().Be("default");
        }
    }
}
