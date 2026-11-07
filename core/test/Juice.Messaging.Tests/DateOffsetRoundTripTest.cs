using System;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Juice.Messaging.Tests
{
    /// <summary>A date is read back with the offset it was written with, whatever the host's offset (#122).</summary>
    public class DateOffsetRoundTripTest
    {
        private readonly IMessageSerializer _serializer;

        // An offset the host is not in, so reading it back in the host's offset shows up on any machine.
        private static readonly TimeSpan Offset = TimeZoneInfo.Local.GetUtcOffset(DateTime.UtcNow) == TimeSpan.FromMinutes(345)
            ? TimeSpan.FromHours(1)
            : TimeSpan.FromMinutes(345);

        private static readonly DateTimeOffset Written = new(2026, 11, 7, 10, 21, 30, Offset);

        public DateOffsetRoundTripTest()
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddMessaging();
            _serializer = services.BuildServiceProvider().GetRequiredService<IMessageSerializer>();
        }

        public record DatedEvent : IntegrationEvent
        {
            public DateTimeOffset At { get; init; }
        }

        private string Payload() => _serializer.Serialize(new DatedEvent { CreatedAt = Written, At = Written });

        private static void ShouldKeepOffset(IMessage? message)
        {
            var dated = message.Should().BeOfType<DatedEvent>().Subject;
            dated.CreatedAt.Should().Be(Written);
            dated.CreatedAt.Offset.Should().Be(Offset);
            dated.At.Offset.Should().Be(Offset);
        }

        [Fact(DisplayName = "A date keeps its offset when the type is known")]
        public void Known_type_keeps_the_offset()
            => ShouldKeepOffset(_serializer.Deserialize<IMessage>(Payload(), typeof(DatedEvent)));

        [Fact(DisplayName = "A date keeps its offset when the type comes from the payload")]
        public void Payload_type_keeps_the_offset()
            => ShouldKeepOffset(_serializer.Deserialize<IMessage>(Payload(), null));

        [Fact(DisplayName = "A date keeps its offset when read as the concrete type")]
        public void Concrete_type_keeps_the_offset()
            => ShouldKeepOffset(_serializer.Deserialize<DatedEvent>(Payload(), null));

        [Fact(DisplayName = "A UTC date stays UTC")]
        public void Utc_stays_utc()
        {
            var utc = new DateTimeOffset(2026, 11, 7, 10, 21, 30, TimeSpan.Zero);
            var payload = _serializer.Serialize(new DatedEvent { CreatedAt = utc });

            var read = _serializer.Deserialize<IMessage>(payload, null);

            read!.CreatedAt.Offset.Should().Be(TimeSpan.Zero);
        }
    }
}
