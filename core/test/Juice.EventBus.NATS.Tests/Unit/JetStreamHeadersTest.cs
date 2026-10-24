using System;
using System.Collections.Generic;
using System.Text;
using FluentAssertions;
using Juice.EventBus.Extensions;
using Xunit;

namespace Juice.EventBus.NATS.Tests.Unit
{
    public class JetStreamHeadersTest
    {
        [Fact]
        public void EncodeDecodeShouldRoundTripAsStrings()
        {
            var timestamp = new DateTimeOffset(2026, 10, 21, 8, 30, 0, TimeSpan.FromHours(7));
            var id = Guid.NewGuid();
            var headers = new Dictionary<string, object?>
            {
                ["x-message-name"] = "ContentPublishedIntegrationEvent",
                ["x-attempts"] = 3,
                ["x-bytes"] = Encoding.UTF8.GetBytes("raw"),
                ["x-time"] = timestamp,
                ["x-message-id"] = id,
                ["x-null"] = null
            };

            var decoded = JetStreamHeaders.Decode(JetStreamHeaders.Encode(headers));

            decoded.Should().NotContainKey("x-null");
            decoded["x-message-name"].Should().Be("ContentPublishedIntegrationEvent");
            decoded["x-attempts"].Should().Be("3");
            decoded["x-bytes"].Should().Be("raw");
            decoded["x-time"].Should().Be(timestamp.ToString("O"));
            decoded["x-message-id"].Should().Be(id.ToString());
        }

        [Fact]
        public void DecodedHeadersShouldWorkWithEventBusHelpers()
        {
            var decoded = JetStreamHeaders.Decode(JetStreamHeaders.Encode(new Dictionary<string, object?>
            {
                ["x-tenant-id"] = "tenant-a",
                ["x-attempts"] = 2
            }));

            decoded.GetHeaderString("x-tenant-id").Should().Be("tenant-a");
            decoded.GetHeaderInt("x-attempts").Should().Be(2);
            decoded.GetHeaderString("missing").Should().BeNull();
        }

        [Fact]
        public void EncodeAndDecodeShouldHandleNull()
        {
            JetStreamHeaders.Encode(null).Should().BeEmpty();
            JetStreamHeaders.Decode(null).Should().BeEmpty();
        }
    }
}
