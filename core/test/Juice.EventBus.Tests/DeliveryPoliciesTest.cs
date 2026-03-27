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

        // US1: processor code policy is used when no global key-matched entry exists
        [Fact(DisplayName = "Processor code policy is used when no global key match exists")]
        public async Task Processor_Code_Policy_Used_When_No_Global_Key_MatchAsync()
        {
            var services = new ServiceCollection();
            services.AddMessaging()
                .AddDelivery(delivery =>
                {
                    delivery.AddDeliveryProcessor<ProcessorPolicyTestContext>("rabbitmq", proc =>
                        proc.AddDeliveryPolicies(opts =>
                            opts.DefaultPolicy = new PolicyConfiguration { BatchSize = 50 }));
                });

            var provider = services.BuildServiceProvider();
            var resolver = provider.GetRequiredService<IDeliveryPolicyResolver>();

            var policy = await resolver.GetPolicyAsync(
                new DeliveryContext("rabbitmq", "send-pending", nameof(ProcessorPolicyTestContext)),
                CancellationToken.None);

            policy.BatchSize.Should().Be(50);
        }

        // US1: global config-section key-matched entry wins over processor code policy
        [Fact(DisplayName = "Global config-section key match overrides processor code policy")]
        public async Task Global_Config_Section_Key_Match_Overrides_Processor_Code_PolicyAsync()
        {
            var contextName = nameof(ProcessorPolicyTestContext);
            var inMemoryConfig = new Dictionary<string, string?>
            {
                [$"GlobalPolicies:Policies:rabbitmq__send-pending__{contextName}:BatchSize"] = "30"
            };
            var cfg = new ConfigurationBuilder().AddInMemoryCollection(inMemoryConfig).Build();

            var services = new ServiceCollection();
            services.AddMessaging()
                .AddDelivery(delivery =>
                {
                    delivery
                        .AddDeliveryPolicies(cfg.GetSection("GlobalPolicies"))
                        .AddDeliveryProcessor<ProcessorPolicyTestContext>("rabbitmq", proc =>
                            proc.AddDeliveryPolicies(opts =>
                                opts.DefaultPolicy = new PolicyConfiguration { BatchSize = 50 }));
                });

            var provider = services.BuildServiceProvider();
            var resolver = provider.GetRequiredService<IDeliveryPolicyResolver>();

            var policy = await resolver.GetPolicyAsync(
                new DeliveryContext("rabbitmq", "send-pending", contextName),
                CancellationToken.None);

            policy.BatchSize.Should().Be(30);
        }

        // US2: processor without code policy falls back to global delegate policy
        [Fact(DisplayName = "Processor without code policy falls back to global policy")]
        public async Task Processor_Without_Code_Policy_Falls_Back_To_GlobalAsync()
        {
            var services = new ServiceCollection();
            services.AddMessaging()
                .AddDelivery(delivery =>
                {
                    delivery
                        .AddDeliveryPolicies(opts =>
                            opts.DefaultPolicy = new PolicyConfiguration { BatchSize = 20 })
                        .AddDeliveryProcessor<ProcessorPolicyTestContext>("rabbitmq");
                });

            var provider = services.BuildServiceProvider();
            var resolver = provider.GetRequiredService<IDeliveryPolicyResolver>();

            var policy = await resolver.GetPolicyAsync(
                new DeliveryContext("rabbitmq", "send-pending", nameof(ProcessorPolicyTestContext)),
                CancellationToken.None);

            policy.BatchSize.Should().Be(20);
        }

        // US2: processor and global both absent — built-in defaults apply
        [Fact(DisplayName = "Processor without any policy uses built-in defaults")]
        public async Task Processor_Without_Any_Policy_Uses_Built_In_DefaultsAsync()
        {
            var services = new ServiceCollection();
            services.AddMessaging()
                .AddDelivery(delivery =>
                {
                    delivery
                        .AddDeliveryPolicies(opts => { }) // register resolver, no policy values set
                        .AddDeliveryProcessor<ProcessorPolicyTestContext>("rabbitmq");
                });

            var provider = services.BuildServiceProvider();
            var resolver = provider.GetRequiredService<IDeliveryPolicyResolver>();

            var policy = await resolver.GetPolicyAsync(
                new DeliveryContext("rabbitmq", "send-pending", nameof(ProcessorPolicyTestContext)),
                CancellationToken.None);

            policy.BatchSize.Should().Be(DeliveryPolicy.Default.BatchSize);
            policy.Interval.Should().Be(DeliveryPolicy.Default.Interval);
        }

        // US3: processor code policy sets only one field; remaining fields come from global default
        [Fact(DisplayName = "Processor code policy partially overrides global and inherits remaining fields")]
        public async Task Processor_Code_Policy_Partially_Overrides_Global_Inherits_RestAsync()
        {
            var services = new ServiceCollection();
            services.AddMessaging()
                .AddDelivery(delivery =>
                {
                    delivery
                        .AddDeliveryPolicies(opts =>
                            opts.DefaultPolicy = new PolicyConfiguration
                            {
                                BatchSize = 10,
                                Interval = TimeSpan.FromSeconds(5)
                            })
                        .AddDeliveryProcessor<ProcessorPolicyTestContext>("rabbitmq", proc =>
                            proc.AddDeliveryPolicies(opts =>
                                opts.DefaultPolicy = new PolicyConfiguration { BatchSize = 50 }));
                });

            var provider = services.BuildServiceProvider();
            var resolver = provider.GetRequiredService<IDeliveryPolicyResolver>();

            var policy = await resolver.GetPolicyAsync(
                new DeliveryContext("rabbitmq", "send-pending", nameof(ProcessorPolicyTestContext)),
                CancellationToken.None);

            policy.BatchSize.Should().Be(50);
            policy.Interval.Should().Be(TimeSpan.FromSeconds(5));
        }

        private sealed class ProcessorPolicyTestContext { }
    }
}
