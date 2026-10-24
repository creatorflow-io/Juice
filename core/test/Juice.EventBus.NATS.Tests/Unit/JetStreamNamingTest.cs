using System;
using System.Collections.Generic;
using FluentAssertions;
using Juice.EventBus.Publishing;
using Xunit;

namespace Juice.EventBus.NATS.Tests.Unit
{
    public class JetStreamNamingTest
    {
        [Theory]
        [InlineData("x.content.integration", "X_CONTENT_INTEGRATION")]
        [InlineData("orders", "ORDERS")]
        [InlineData("my-app.events", "MY_APP_EVENTS")]
        public void DeriveStreamNameShouldUpperCaseAndReplaceSeparators(string destination, string expected)
        {
            JetStreamNaming.DeriveStreamName(destination).Should().Be(expected);
        }

        [Fact]
        public void ParkingNamesShouldFollowConvention()
        {
            JetStreamNaming.ParkingStreamName("X_CONTENT").Should().Be("X_CONTENT_PARKING");
            JetStreamNaming.DefaultParkingPrefix("x.content").Should().Be("parking.x.content");
        }

        [Fact]
        public void BuildSubjectShouldJoinDestinationAndRoutingKey()
        {
            JetStreamNaming.BuildSubject("x.content.integration", "ContentPublishedIntegrationEvent")
                .Should().Be("x.content.integration.ContentPublishedIntegrationEvent");
        }

        [Theory]
        [InlineData("OrderCreated", "d.OrderCreated")]
        [InlineData("kernel.*", "d.kernel.*")]
        [InlineData("kernel.#", "d.kernel.>")]
        [InlineData("#", "d.>")]
        [InlineData("#.retry.*", "d.>")]
        public void ToFilterSubjectShouldTranslateTopicRoutes(string route, string expected)
        {
            JetStreamNaming.ToFilterSubject("d", route).Should().Be(expected);
        }

        [Fact]
        public void ExtractEventNameShouldRemoveDestinationPrefix()
        {
            JetStreamNaming.ExtractEventName("x.content.kernel.audit", "x.content").Should().Be("kernel.audit");
            JetStreamNaming.ExtractEventName("other.Event", "x.content").Should().Be("other.Event");
        }

        [Theory]
        [InlineData("a b")]
        [InlineData("a.*")]
        [InlineData("a.>")]
        [InlineData("a..b")]
        [InlineData(".a")]
        [InlineData("")]
        public void ValidateTokenShouldRejectInvalidValues(string value)
        {
            var act = () => JetStreamNaming.ValidateToken(value, "value");
            act.Should().Throw<ArgumentException>();
        }

        [Fact]
        public void ValidateTokenShouldAcceptDottedTokens()
        {
            var act = () => JetStreamNaming.ValidateToken("x.content.integration", "value");
            act.Should().NotThrow();
        }

        [Fact]
        public void ResolveRoutingKeyShouldFollowPrecedence()
        {
            var headers = new Dictionary<string, object?>
            {
                ["x-routing-key"] = "rk",
                ["x-message-name"] = "name",
                ["x-message-type"] = "type"
            };
            JetStreamNaming.ResolveRoutingKey(new PublishContext("1", Headers: headers, RoutingKey: "explicit"))
                .Should().Be("explicit");
            JetStreamNaming.ResolveRoutingKey(new PublishContext("1", Headers: headers))
                .Should().Be("rk");

            headers.Remove("x-routing-key");
            JetStreamNaming.ResolveRoutingKey(new PublishContext("1", Headers: headers))
                .Should().Be("name");

            headers.Remove("x-message-name");
            JetStreamNaming.ResolveRoutingKey(new PublishContext("1", Headers: headers))
                .Should().Be("type");
        }

        [Fact]
        public void ResolveRoutingKeyShouldThrowWhenMissing()
        {
            var act = () => JetStreamNaming.ResolveRoutingKey(new PublishContext("1"));
            act.Should().Throw<InvalidOperationException>().WithMessage("*x-message-name*");
        }
    }
}
