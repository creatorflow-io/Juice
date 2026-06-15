using System;
using System.Linq;
using FluentAssertions;
using Juice.Messaging;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Juice.EventBus.Tests
{
    /// <summary>
    /// Unit tests for the ProcessedBy host/app identity tracking feature.
    /// </summary>
    public class ProcessedByTests
    {
        // â"€â"€â"€ T015/T016 â€" US1: host identity populated after delivery attempt â"€â"€â"€â"€

        [Fact(DisplayName = "NodeIdentity.NodeId has MachineName:ProcessId format")]
        public void NodeId_Has_Expected_FormatAsync()
        {
            var services = new ServiceCollection();
            services.AddMessaging()
                .AddDelivery(_ => { });

            var provider = services.BuildServiceProvider();
            var identity = provider.GetRequiredService<INodeIdentity>();

            identity.NodeId.Should().NotBeNullOrEmpty();

            var expected = $"{System.Environment.MachineName}:{System.Environment.ProcessId}";
            identity.NodeId.Should().Be(expected,
                "NodeId must uniquely identify this process as MachineName:ProcessId");
        }

        [Fact(DisplayName = "NodeIdentity.NodeId is stable within process lifetime")]
        public void NodeId_Is_Stable_Within_Process_LifetimeAsync()
        {
            var services = new ServiceCollection();
            services.AddMessaging()
                .AddDelivery(_ => { });

            var provider = services.BuildServiceProvider();

            var id1 = provider.GetRequiredService<INodeIdentity>().NodeId;
            var id2 = provider.GetRequiredService<INodeIdentity>().NodeId;

            id1.Should().Be(id2, "NodeId must be deterministic for the lifetime of the process (singleton)");
        }

        // â"€â"€â"€ T017 â€" US2: two distinct node identities produce distinct NodeIds â"€â"€

        [Fact(DisplayName = "Two custom INodeIdentity registrations produce distinct NodeIds")]
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
            services.AddMessaging().UseNodeIdentity(customId).AddDelivery(_ => { });

            var provider = services.BuildServiceProvider();

            provider.GetRequiredService<INodeIdentity>().NodeId
                .Should().Be(customId);
        }

        [Fact(DisplayName = "UseNodeIdentity overrides default registration")]
        public void UseNodeIdentity_Overrides_DefaultAsync()
        {
            const string customId = "custom-host:9999";

            var services = new ServiceCollection();
            services.AddMessaging().UseNodeIdentity(new FixedNodeIdentity(customId)).AddDelivery(_ => { });

            var provider = services.BuildServiceProvider();

            var identity = provider.GetRequiredService<INodeIdentity>();
            identity.NodeId.Should().Be(customId,
                "UseNodeIdentity must replace the default NodeIdentity");
        }

        [Fact(DisplayName = "UseNodeIdentity<T> overrides default registration via type parameter")]
        public void UseNodeIdentity_Generic_Overrides_DefaultAsync()
        {
            var services = new ServiceCollection();
            services.AddMessaging().UseNodeIdentity<FixedNodeIdentity>().AddDelivery(_ => { });

            var provider = services.BuildServiceProvider();

            var identity = provider.GetRequiredService<INodeIdentity>();
            identity.Should().BeOfType<FixedNodeIdentity>();
        }

        // â"€â"€â"€ Validation â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€

        [Fact(DisplayName = "UseNodeIdentity(string) throws on null or empty")]
        public void UseNodeIdentity_String_Throws_On_NullOrEmpty()
        {
            var messagingBuilder = new ServiceCollection().AddMessaging();

            messagingBuilder.Invoking(m => m.UseNodeIdentity(string.Empty))
                .Should().Throw<ArgumentException>();

            messagingBuilder.Invoking(m => m.UseNodeIdentity("   "))
                .Should().Throw<ArgumentException>();
        }

        [Fact(DisplayName = "UseNodeIdentity(string) throws when NodeId exceeds NameLength")]
        public void UseNodeIdentity_String_Throws_When_Too_Long()
        {
            var messagingBuilder = new ServiceCollection().AddMessaging();

            var tooLong = new string('a', 257); // exceeds NameLength (256)
            messagingBuilder.Invoking(m => m.UseNodeIdentity(tooLong))
                .Should().Throw<ArgumentException>()
                .WithMessage("*exceeds the maximum allowed length*");
        }

        // â"€â"€â"€ Helpers â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€

        private sealed class FixedNodeIdentity : INodeIdentity
        {
            public FixedNodeIdentity() { }
            public FixedNodeIdentity(string nodeId) { NodeId = nodeId; }
            public string NodeId { get; } = "fixed-node:0";
        }
    }
}
