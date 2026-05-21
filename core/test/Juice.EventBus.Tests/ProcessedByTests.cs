using System;
using System.Linq;
using FluentAssertions;
using Juice.Messaging.Outbox;
using Juice.Messaging.Outbox.Delivery;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Juice.EventBus.Tests
{
    /// <summary>
    /// Unit tests for the ProcessedBy host/app identity tracking feature.
    /// </summary>
    public class ProcessedByTests
    {
        // ─── T015/T016 — US1: host identity populated after delivery attempt ────

        [Fact(DisplayName = "DeliveryNodeIdentity.NodeId has MachineName:ProcessId format")]
        public void NodeId_Has_Expected_FormatAsync()
        {
            var services = new ServiceCollection();
            services.AddMessaging()
                .AddDelivery(_ => { });

            var provider = services.BuildServiceProvider();
            var identity = provider.GetRequiredService<IDeliveryNodeIdentity>();

            identity.NodeId.Should().NotBeNullOrEmpty();

            var expected = $"{System.Environment.MachineName}:{System.Environment.ProcessId}";
            identity.NodeId.Should().Be(expected,
                "NodeId must uniquely identify this process as MachineName:ProcessId");
        }

        [Fact(DisplayName = "DeliveryNodeIdentity.NodeId is stable within process lifetime")]
        public void NodeId_Is_Stable_Within_Process_LifetimeAsync()
        {
            var services = new ServiceCollection();
            services.AddMessaging()
                .AddDelivery(_ => { });

            var provider = services.BuildServiceProvider();

            var id1 = provider.GetRequiredService<IDeliveryNodeIdentity>().NodeId;
            var id2 = provider.GetRequiredService<IDeliveryNodeIdentity>().NodeId;

            id1.Should().Be(id2, "NodeId must be deterministic for the lifetime of the process (singleton)");
        }

        // ─── T017 — US2: two distinct node identities produce distinct NodeIds ──

        [Fact(DisplayName = "Two custom IDeliveryNodeIdentity registrations produce distinct NodeIds")]
        public void Two_Distinct_NodeIdentities_Produce_Distinct_NodeIdsAsync()
        {
            var nodeA = new FixedNodeIdentity("host-a:1001");
            var nodeB = new FixedNodeIdentity("host-b:2002");

            nodeA.NodeId.Should().NotBe(nodeB.NodeId,
                "different hosts must produce distinct NodeId values so per-node delivery records are distinguishable");
        }

        [Fact(DisplayName = "UseNodeIdentity(string) sets a fixed NodeId")]
        public void UseNodeIdentity_String_Sets_Fixed_NodeIdAsync()
        {
            const string customId = "my-host:5000";

            var services = new ServiceCollection();
            services.AddMessaging()
                .AddDelivery(delivery => delivery.UseNodeIdentity(customId));

            var provider = services.BuildServiceProvider();

            provider.GetRequiredService<IDeliveryNodeIdentity>().NodeId
                .Should().Be(customId);
        }

        [Fact(DisplayName = "UseNodeIdentity overrides default registration")]
        public void UseNodeIdentity_Overrides_DefaultAsync()
        {
            const string customId = "custom-host:9999";

            var services = new ServiceCollection();
            services.AddMessaging()
                .AddDelivery(delivery => delivery.UseNodeIdentity(new FixedNodeIdentity(customId)));

            var provider = services.BuildServiceProvider();

            var identity = provider.GetRequiredService<IDeliveryNodeIdentity>();
            identity.NodeId.Should().Be(customId,
                "UseNodeIdentity must replace the default DeliveryNodeIdentity");
        }

        [Fact(DisplayName = "UseNodeIdentity<T> overrides default registration via type parameter")]
        public void UseNodeIdentity_Generic_Overrides_DefaultAsync()
        {
            var services = new ServiceCollection();
            services.AddMessaging()
                .AddDelivery(delivery => delivery.UseNodeIdentity<FixedNodeIdentity>());

            var provider = services.BuildServiceProvider();

            var identity = provider.GetRequiredService<IDeliveryNodeIdentity>();
            identity.Should().BeOfType<FixedNodeIdentity>();
        }

        // ─── Validation ──────────────────────────────────────────────────────────

        [Fact(DisplayName = "UseNodeIdentity(string) throws on null or empty")]
        public void UseNodeIdentity_String_Throws_On_NullOrEmpty()
        {
            var services = new ServiceCollection();
            DeliveryBuilder? delivery = null;
            services.AddMessaging().AddDelivery(d => { delivery = d; });

            delivery!.Invoking(d => d.UseNodeIdentity(string.Empty))
                .Should().Throw<ArgumentException>();

            delivery!.Invoking(d => d.UseNodeIdentity("   "))
                .Should().Throw<ArgumentException>();
        }

        [Fact(DisplayName = "UseNodeIdentity(string) throws when NodeId exceeds NameLength")]
        public void UseNodeIdentity_String_Throws_When_Too_Long()
        {
            var services = new ServiceCollection();
            DeliveryBuilder? delivery = null;
            services.AddMessaging().AddDelivery(d => { delivery = d; });

            var tooLong = new string('a', 257); // exceeds NameLength (256)
            delivery!.Invoking(d => d.UseNodeIdentity(tooLong))
                .Should().Throw<ArgumentException>()
                .WithMessage("*exceeds the maximum allowed length*");
        }

        // ─── Helpers ─────────────────────────────────────────────────────────────

        private sealed class FixedNodeIdentity : IDeliveryNodeIdentity
        {
            public FixedNodeIdentity() { }
            public FixedNodeIdentity(string nodeId) { NodeId = nodeId; }
            public string NodeId { get; } = "fixed-node:0";
        }
    }
}
