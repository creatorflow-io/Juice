using System;
using System.Linq;
using FluentAssertions;
using Juice.EventBus.NATS.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NATS.Client.JetStream.Models;
using Xunit;

namespace Juice.EventBus.NATS.Tests.Unit
{
    public class JetStreamInfrastructureBuilderTest
    {
        [Fact]
        public void DeclareStreamShouldApplyDefaults()
        {
            var builder = new JetStreamInfrastructureBuilder("nats").DeclareStream("x.content");

            var stream = builder.Streams.Should().ContainSingle().Subject;
            stream.Name.Should().Be("X_CONTENT");
            stream.Subjects.Should().Equal("x.content.>");
            stream.Storage.Should().Be(JetStreamStorage.File);
            stream.Retention.Should().Be(JetStreamRetention.Limits);
            stream.DuplicateWindow.Should().Be(TimeSpan.FromMinutes(10));
            stream.Replicas.Should().Be(1);
            stream.MaxAge.Should().BeNull();

            var config = stream.ToStreamConfig();
            config.Name.Should().Be("X_CONTENT");
            config.Subjects.Should().Equal("x.content.>");
            config.Storage.Should().Be(StreamConfigStorage.File);
            config.Retention.Should().Be(StreamConfigRetention.Limits);
            config.NumReplicas.Should().Be(1);
            config.DuplicateWindow.Should().Be(TimeSpan.FromMinutes(10));
        }

        [Fact]
        public void DeclareParkingStreamShouldApplyDefaults()
        {
            var builder = new JetStreamInfrastructureBuilder("nats")
                .DeclareStream("x.content")
                .DeclareParkingStream("x.content");

            var parking = builder.Streams.Single(s => s.Name.EndsWith("_PARKING"));
            parking.Name.Should().Be("X_CONTENT_PARKING");
            parking.Subjects.Should().Equal("parking.x.content.>");
            parking.MaxAge.Should().Be(TimeSpan.FromDays(14));
            parking.ToStreamConfig().MaxAge.Should().Be(TimeSpan.FromDays(14));
        }

        [Fact]
        public void ConfigureShouldOverrideDefaults()
        {
            var builder = new JetStreamInfrastructureBuilder("nats")
                .DeclareStream("x.content", s =>
                {
                    s.Storage = JetStreamStorage.Memory;
                    s.MaxAge = TimeSpan.FromHours(1);
                    s.MaxMsgs = 100;
                    s.Replicas = 3;
                });

            var config = builder.Streams.Single().ToStreamConfig();
            config.Storage.Should().Be(StreamConfigStorage.Memory);
            config.MaxAge.Should().Be(TimeSpan.FromHours(1));
            config.MaxMsgs.Should().Be(100);
            config.NumReplicas.Should().Be(3);
        }

        [Fact]
        public void OverlappingSubjectsShouldThrow()
        {
            var act = () => new JetStreamInfrastructureBuilder("nats")
                .DeclareStream("x.content")
                .DeclareStream("x.content.vip", s => s.Name = "VIP");

            act.Should().Throw<InvalidOperationException>().WithMessage("*overlaps*X_CONTENT*");
        }

        [Theory]
        [InlineData("a.>", "a.b.c", true)]
        [InlineData("a.*", "a.b", true)]
        [InlineData("a.*", "a.b.c", false)]
        [InlineData("a.b", "a.c", false)]
        [InlineData("parking.x.>", "x.>", false)]
        public void SubjectsOverlapShouldFollowWildcards(string a, string b, bool expected)
        {
            JetStreamInfrastructureBuilder.SubjectsOverlap(a, b).Should().Be(expected);
            JetStreamInfrastructureBuilder.SubjectsOverlap(b, a).Should().Be(expected);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(6)]
        public void InvalidReplicasShouldThrow(int replicas)
        {
            var act = () => new JetStreamInfrastructureBuilder("nats").DeclareStream("x.content", s => s.Replicas = replicas);
            act.Should().Throw<ArgumentOutOfRangeException>();
        }

        [Fact]
        public void WorkQueueRetentionShouldLogWarning()
        {
            var logger = new JetStreamConnectionOptionsTest.TestLogger();
            var services = new ServiceCollection();
            services.AddSingleton<ILoggerFactory>(new SingleLoggerFactory(logger));
            services.AddSingleton(typeof(ILogger<>), typeof(Logger<>));
            services.AddEventBus()
                .AddJetStream(js => js
                    .AddConnection("nats", o => { })
                    .AddInfrastructureTopology("nats", t => t.DeclareStream("x.jobs", s => s.Retention = JetStreamRetention.WorkQueue)));

            using var sp = services.BuildServiceProvider();
            sp.GetRequiredService<JetStreamInfrastructureInitializer>();

            logger.Warnings.Should().ContainSingle(w => w.Contains("X_JOBS") && w.Contains("WorkQueue"));
        }

        private sealed class SingleLoggerFactory : ILoggerFactory
        {
            private readonly ILogger _logger;
            public SingleLoggerFactory(ILogger logger) => _logger = logger;
            public void AddProvider(ILoggerProvider provider) { }
            public ILogger CreateLogger(string categoryName) => _logger;
            public void Dispose() { }
        }
    }
}
