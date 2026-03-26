using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Juice.Messaging.Outbox.Delivery;
using Juice.Messaging.Outbox.Delivery.Internal;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Juice.EventBus.Tests
{
    public class DeliveryPoliciesTest
    {
        private readonly IConfiguration configuration;

        public DeliveryPoliciesTest()
        {
            var serviceCollection = new ServiceCollection();
            serviceCollection.AddLogging();
            serviceCollection.AddOptions();
            
            // Build a temporary service provider
            var serviceProvider = serviceCollection.BuildServiceProvider();
            
            // Create a configuration object
            var configBuilder = new ConfigurationBuilder()
                .SetBasePath(AppContext.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                .AddEnvironmentVariables();
            
            configuration = configBuilder.Build();
        }
        
        [Fact(DisplayName = "Should apply custom policy")]
        public async Task Should_Apply_Custom_Policy_For_Publisher_IntentAsync()
        {
            // Arrange
            var services = new ServiceCollection();
            services.AddMessaging()
                .AddDelivery(delivery => {
                    delivery.AddDeliveryPolicies(configuration.GetSection("Juice:EventBus:DeliveryPolicies"));
                })
                ;
            
            var provider = services.BuildServiceProvider();
            var resolver = provider.GetRequiredService<IDeliveryPolicyResolver>();
            
            // Act
            var policy = await resolver.GetPolicyAsync(
                new DeliveryContext("rabbitmq", "send-pending", "TestContext"), 
                CancellationToken.None);
            
            // Assert
            policy.BatchSize.Should().Be(20); // From configuration
            policy.Interval.Should().Be(TimeSpan.FromSeconds(3));
        }

        [Fact(DisplayName = "Should apply exponmential backoff on retry")]
        public void Should_Apply_Exponential_Backoff_On_Retry()
        {
            // Test retry delays
            var policy = DeliveryPolicy.Default;

            var retry1 = policy.GetNextAttempt(1); // 5s
            var retry2 = policy.GetNextAttempt(2); // 10s
            var retry3 = policy.GetNextAttempt(3); // 20s

            // Assert backoff timing
        }

        [Fact(DisplayName = "Multiple AddDeliveryPolicies(delegate) calls merge policies")]
        public async Task Multiple_Delegate_Calls_Merge_PoliciesAsync()
        {
            var services = new ServiceCollection();
            services.AddMessaging()
                .AddDelivery(delivery =>
                {
                    delivery
                        .AddDeliveryPolicies(o =>
                        {
                            o.Policies["rabbitmq:send-pending:*"] = new PolicyConfiguration { BatchSize = 5 };
                        })
                        .AddDeliveryPolicies(o =>
                        {
                            o.Policies["rabbitmq:retry:*"] = new PolicyConfiguration { BatchSize = 15 };
                        });
                });

            var provider = services.BuildServiceProvider();
            var resolver = provider.GetRequiredService<IDeliveryPolicyResolver>();

            var sendPolicy = await resolver.GetPolicyAsync(
                new DeliveryContext("rabbitmq", "send-pending", "Ctx"), CancellationToken.None);
            var retryPolicy = await resolver.GetPolicyAsync(
                new DeliveryContext("rabbitmq", "retry", "Ctx"), CancellationToken.None);

            sendPolicy.BatchSize.Should().Be(5);
            retryPolicy.BatchSize.Should().Be(15);
        }

        [Fact(DisplayName = "Delegate call and config section call merge policies")]
        public async Task Delegate_And_Config_Calls_Merge_PoliciesAsync()
        {
            var inMemoryConfig = new Dictionary<string, string?>
            {
                ["DeliveryPolicies:Policies:rabbitmq__send-pending__*:BatchSize"] = "7"
            };
            var cfg = new ConfigurationBuilder().AddInMemoryCollection(inMemoryConfig).Build();

            var services = new ServiceCollection();
            services.AddMessaging()
                .AddDelivery(delivery =>
                {
                    delivery
                        .AddDeliveryPolicies(cfg.GetSection("DeliveryPolicies"))
                        .AddDeliveryPolicies(o =>
                        {
                            o.Policies["rabbitmq:retry:*"] = new PolicyConfiguration { BatchSize = 25 };
                        });
                });

            var provider = services.BuildServiceProvider();
            var resolver = provider.GetRequiredService<IDeliveryPolicyResolver>();

            var sendPolicy = await resolver.GetPolicyAsync(
                new DeliveryContext("rabbitmq", "send-pending", "Ctx"), CancellationToken.None);
            var retryPolicy = await resolver.GetPolicyAsync(
                new DeliveryContext("rabbitmq", "retry", "Ctx"), CancellationToken.None);

            sendPolicy.BatchSize.Should().Be(7);
            retryPolicy.BatchSize.Should().Be(25);
        }
    }
}
