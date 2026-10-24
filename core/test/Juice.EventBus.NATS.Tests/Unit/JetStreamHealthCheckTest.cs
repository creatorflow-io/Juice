using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Juice.EventBus.NATS.HealthChecks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using NATS.Client.Core;
using NATS.Client.JetStream;
using Xunit;

namespace Juice.EventBus.NATS.Tests.Unit
{
    public class JetStreamHealthCheckTest
    {
        private sealed class FakeConnection : IJetStreamConnection
        {
            public string Name => "nats";
            public INatsConnection Connection => throw new NotSupportedException();
            public INatsJSContext JetStream => throw new NotSupportedException();
            public NatsConnectionState State { get; set; }
            public ValueTask EnsureConnectedAsync(CancellationToken cancellationToken = default) => default;
        }

        private static async Task<HealthStatus> CheckAsync(NatsConnectionState? state)
        {
            var services = new ServiceCollection();
            if (state.HasValue)
            {
                services.AddKeyedSingleton<IJetStreamConnection>("nats", new FakeConnection { State = state.Value });
            }
            using var sp = services.BuildServiceProvider();
            var check = new JetStreamHealthCheck(sp, "nats");
            var result = await check.CheckHealthAsync(new HealthCheckContext());
            return result.Status;
        }

        [Theory]
        [InlineData(NatsConnectionState.Open, HealthStatus.Healthy)]
        [InlineData(NatsConnectionState.Reconnecting, HealthStatus.Degraded)]
        [InlineData(NatsConnectionState.Closed, HealthStatus.Unhealthy)]
        [InlineData(NatsConnectionState.Connecting, HealthStatus.Unhealthy)]
        [InlineData(NatsConnectionState.Failed, HealthStatus.Unhealthy)]
        public async Task StateShouldMapToHealthStatusAsync(NatsConnectionState state, HealthStatus expected)
        {
            (await CheckAsync(state)).Should().Be(expected);
        }

        [Fact]
        public async Task UnregisteredConnectionShouldBeUnhealthyAsync()
        {
            (await CheckAsync(null)).Should().Be(HealthStatus.Unhealthy);
        }

        [Fact]
        public void RegistrationShouldUseDefaultNameAndTags()
        {
            var services = new ServiceCollection();
            services.AddHealthChecks().AddJetStreamHealthCheck("nats");
            using var sp = services.BuildServiceProvider();

            var registration = sp.GetRequiredService<IOptions<HealthCheckServiceOptions>>().Value.Registrations.Single();
            registration.Name.Should().Be("nats_nats");
            registration.Tags.Should().BeEquivalentTo("nats", "messaging");
        }
    }
}
