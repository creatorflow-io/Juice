using FluentAssertions;
using Juice.Messaging.Idempotency;

namespace Juice.Messaging.Idempotency.Tests
{
    public class IdempotencyOptionsTests
    {
        [Fact]
        public void Defaults_should_match_specification()
        {
            var options = new IdempotencyOptions();

            options.InFlightTtl.Should().Be(TimeSpan.FromMinutes(15));
            options.CompletedRetention.Should().Be(TimeSpan.FromHours(24));
            options.MaxKeyLength.Should().Be(128);
            options.PurgeInterval.Should().Be(TimeSpan.FromMinutes(5));
        }
    }
}
