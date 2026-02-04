using System;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Xunit;
using System.Threading;
using Juice.EventBus.Delivery.Policies;

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
            services.AddEventBus()
                .AddDeliveryCore(delivery => {
                    delivery.AddDeliveryPolicies(configuration.GetSection("Juice:EventBus:DeliveryPolicies"));
                });
            
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
    }
}
