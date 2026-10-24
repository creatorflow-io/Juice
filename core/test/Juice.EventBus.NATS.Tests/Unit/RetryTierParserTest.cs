using System;
using FluentAssertions;
using Juice.EventBus.Extensions;
using Juice.EventBus.NATS.Policies;
using Xunit;

namespace Juice.EventBus.NATS.Tests.Unit
{
    public class RetryTierParserTest
    {
        [Theory]
        [InlineData("10s", 10_000)]
        [InlineData("1m", 60_000)]
        [InlineData("5m", 300_000)]
        [InlineData("1h", 3_600_000)]
        [InlineData("500ms", 500)]
        [InlineData("content.retry.10s", 10_000)]
        [InlineData("x.logs.retry.1s", 1_000)]
        public void ParseShouldReadLastToken(string tier, int expectedMs)
        {
            RetryTierParser.Parse(tier).Should().Be(TimeSpan.FromMilliseconds(expectedMs));
        }

        [Theory]
        [InlineData("abc")]
        [InlineData("")]
        [InlineData("-1s")]
        [InlineData("10")]
        [InlineData("content.retry.")]
        public void ParseShouldRejectInvalidTiers(string tier)
        {
            var act = () => RetryTierParser.Parse(tier);
            act.Should().Throw<FormatException>().WithMessage($"*'{tier}'*");
        }

        [Theory]
        [InlineData(250)]
        [InlineData(1_000)]
        [InlineData(90_000)]
        [InlineData(7_200_000)]
        public void ParseShouldRoundTripShortNames(int milliseconds)
        {
            var span = TimeSpan.FromMilliseconds(milliseconds);
            RetryTierParser.Parse(span.ToShortName()).Should().Be(span);
        }
    }
}
