using System;
using System.Threading.Tasks;
using FluentAssertions;
using Juice.EventBus.NATS.Policies;
using Juice.EventBus.Policies;
using Microsoft.Extensions.Options;
using Xunit;

namespace Juice.EventBus.NATS.Tests.Unit
{
    public class JetStreamRetryPolicyProviderTest
    {
        private static JetStreamRetryPolicyProvider Provider(ConsumeRetryPolicyOptions options)
            => new(Options.Create(options));

        private static ConsumeRetryPolicyOptions DefaultTiers(params string[] tiers)
            => new() { Default = new ConsumeRetryPolicyDefinition { RetryTiers = tiers, IsParkingEnabled = true } };

        [Fact]
        public async Task AttemptsShouldMapToTierDelaysAsync()
        {
            var provider = Provider(DefaultTiers("10s", "1m", "content.retry.5m"));

            (await provider.GetRetryPolicyForSourceAsync("x.content", 1))!.Delay.Should().Be(TimeSpan.FromSeconds(10));
            (await provider.GetRetryPolicyForSourceAsync("x.content", 2))!.Delay.Should().Be(TimeSpan.FromMinutes(1));
            var third = (await provider.GetRetryPolicyForSourceAsync("x.content", 3))!;
            third.Delay.Should().Be(TimeSpan.FromMinutes(5));
            third.IsMaxRetryReached.Should().BeFalse();
            third.LastDelay.Should().Be(TimeSpan.FromMinutes(5));
        }

        [Fact]
        public async Task AttemptAfterLastTierShouldReachMaxAsync()
        {
            var provider = Provider(DefaultTiers("10s", "1m"));

            var policy = (await provider.GetRetryPolicyForSourceAsync("x.content", 3))!;
            policy.IsMaxRetryReached.Should().BeTrue();
            policy.Delay.Should().BeNull();
            policy.IsParkingEnabled.Should().BeTrue();
        }

        [Fact]
        public async Task MaxRetryAttemptsShouldCapTiersAsync()
        {
            var options = DefaultTiers("10s", "1m", "5m");
            options.Default!.MaxRetryAttempts = 1;
            var provider = Provider(options);

            (await provider.GetRetryPolicyForSourceAsync("x.content", 1))!.Delay.Should().Be(TimeSpan.FromSeconds(10));
            (await provider.GetRetryPolicyForSourceAsync("x.content", 2))!.IsMaxRetryReached.Should().BeTrue();
        }

        [Fact]
        public async Task SourcePolicyShouldTakePriorityOverDefaultAsync()
        {
            var options = DefaultTiers("10s");
            options.AddPolicy(new ConsumeRetryPolicyDefinition
            {
                Source = "orders",
                DeadLetterDest = "parking.orders",
                RetryTiers = ["orders.retry.1m"],
                IsParkingEnabled = false
            });
            var provider = Provider(options);

            var orders = (await provider.GetRetryPolicyForSourceAsync("ORDERS", 1))!;
            orders.Delay.Should().Be(TimeSpan.FromMinutes(1));
            orders.ParkingPrefix.Should().Be("parking.orders");
            orders.IsParkingEnabled.Should().BeFalse();

            var other = (await provider.GetRetryPolicyForSourceAsync("x.content", 1))!;
            other.Delay.Should().Be(TimeSpan.FromSeconds(10));
            other.ParkingPrefix.Should().BeNull();
        }

        [Fact]
        public async Task NoPolicyShouldReturnNullAsync()
        {
            var provider = Provider(new ConsumeRetryPolicyOptions());
            (await provider.GetRetryPolicyForSourceAsync("x.content", 1)).Should().BeNull();
        }

        [Fact]
        public async Task InvalidAttemptsOrSourceShouldReturnNullAsync()
        {
            var provider = Provider(DefaultTiers("10s"));
            (await provider.GetRetryPolicyForSourceAsync("x.content", 0)).Should().BeNull();
            (await provider.GetRetryPolicyForSourceAsync(null, 1)).Should().BeNull();
        }

        [Fact]
        public async Task InvalidTierShouldFailOnUseWithSourceAsync()
        {
            var options = new ConsumeRetryPolicyOptions();
            options.AddPolicy(new ConsumeRetryPolicyDefinition { Source = "orders", RetryTiers = ["soon"] });

            var provider = Provider(options);
            var act = async () => await provider.GetRetryPolicyForSourceAsync("orders", 1);
            await act.Should().ThrowAsync<FormatException>().WithMessage("*'soon'*'orders'*");
        }

        [Fact]
        public async Task RabbitMQPolicyTiersShouldNotBreakOtherSourcesAsync()
        {
            // RabbitMQ tiers are routing keys; they share ConsumeRetryPolicyOptions with JetStream
            var options = DefaultTiers("10s");
            options.AddPolicy(new ConsumeRetryPolicyDefinition { Source = "x.logs", RetryTiers = ["retry.1s", "retry.1s1"] });

            var provider = Provider(options);
            var policy = await provider.GetRetryPolicyForSourceAsync("x.content", 1);

            policy!.Delay.Should().Be(TimeSpan.FromSeconds(10));
        }
    }
}
