using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Juice.EF.Tests.Events;
using Juice.EventBus.NATS.Consuming;
using Juice.EventBus.Publishing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NATS.Client.Core;
using Xunit;

namespace Juice.EventBus.NATS.Tests.Unit
{
    public class JetStreamBuilderTest
    {
        [Fact]
        public void ProducerShouldResolveByKeyWithoutConnecting()
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddEventBus()
                .AddJetStream(js => js
                    .AddConnection("nats", o => o.Url = "nats://localhost:4222")
                    .AddProducer("nats", "nats"));

            using var sp = services.BuildServiceProvider();
            var publisher = sp.GetRequiredKeyedService<ITransportPublisher>("nats");

            publisher.Key.Should().Be("nats");
            sp.GetRequiredKeyedService<IJetStreamConnection>("nats").State.Should().Be(NatsConnectionState.Closed);
        }

        [Fact]
        public void DuplicateProducerKeyShouldThrow()
        {
            var services = new ServiceCollection();
            var act = () => services.AddEventBus()
                .AddJetStream(js => js
                    .AddConnection("nats", o => { })
                    .AddProducer("nats", "nats")
                    .AddProducer("nats", "nats"));

            act.Should().Throw<InvalidOperationException>().WithMessage("*'nats'*already*");
        }

        [Fact]
        public void ProducerWithUnregisteredConnectionShouldThrowWhenResolved()
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddEventBus()
                .AddJetStream(js => js.AddProducer("nats", "missing"));

            using var sp = services.BuildServiceProvider();
            var act = () => sp.GetRequiredKeyedService<ITransportPublisher>("nats");

            act.Should().Throw<InvalidOperationException>().WithMessage("*'missing'*not registered*");
        }

        [Fact]
        public void DuplicateConsumerShouldThrow()
        {
            var services = new ServiceCollection();
            var act = () => services.AddEventBus()
                .AddJetStream(js => js
                    .AddConsumer("a", "X_CONTENT", "durable", "nats", c => c.Destination("x.content"))
                    .AddConsumer("b", "X_CONTENT", "durable", "nats", c => c.Destination("x.content")));

            act.Should().Throw<InvalidOperationException>().WithMessage("*'durable'*'X_CONTENT'*already*");
        }

        [Fact]
        public void SubscribeShouldRegisterHandlerAsTransient()
        {
            var services = new ServiceCollection();
            services.AddEventBus()
                .AddJetStream(js => js
                    .AddConsumer("a", "X_CONTENT", "durable", "nats", c => c
                        .Destination("x.content")
                        .Subscribe<ContentPublishedIntegrationEvent, ProbeHandler>()));

            services.Should().ContainSingle(d => d.ServiceType == typeof(ProbeHandler))
                .Which.Lifetime.Should().Be(ServiceLifetime.Transient);
        }

        [Fact]
        public void MissingDestinationShouldThrowWhenResolved()
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddEventBus()
                .AddJetStream(js => js
                    .AddConnection("nats", o => { })
                    .AddConsumer("a", "X_CONTENT", "durable", "nats", c => c
                        .Subscribe<ContentPublishedIntegrationEvent, ProbeHandler>()));

            using var sp = services.BuildServiceProvider();
            var act = () => sp.GetServices<IHostedService>().ToList();

            act.Should().Throw<InvalidOperationException>().WithMessage("*Destination*");
        }

        [Theory]
        [InlineData(null, "x.content.ContentPublishedIntegrationEvent")]
        [InlineData("kernel.*", "x.content.kernel.*")]
        [InlineData("kernel.#", "x.content.kernel.>")]
        [InlineData("#.retry.*", "x.content.>")]
        public void FilterSubjectsShouldFollowRoutes(string? route, string expected)
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddEventBus()
                .AddJetStream(js => js
                    .AddConnection("nats", o => { })
                    .AddConsumer("a", "X_CONTENT", "durable", "nats", c => c
                        .Destination("x.content")
                        .Subscribe<ContentPublishedIntegrationEvent, ProbeHandler>(route)));

            using var sp = services.BuildServiceProvider();
            var hosted = sp.GetServices<IHostedService>().OfType<JetStreamConsumerHostedService>().Single();

            hosted.Endpoint.FilterSubjects.Should().Equal(expected);
        }

        [Fact]
        public void FilterSubjectsShouldCollapseWhenAnyRouteNeedsAll()
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddEventBus()
                .AddJetStream(js => js
                    .AddConnection("nats", o => { })
                    .AddConsumer("a", "X_CONTENT", "durable", "nats", c => c
                        .Destination("x.content")
                        .Subscribe<ContentPublishedIntegrationEvent, ProbeHandler>("kernel.*")
                        .Subscribe<ContentPublishedIntegrationEvent, ProbeHandler>("#")));

            using var sp = services.BuildServiceProvider();
            var hosted = sp.GetServices<IHostedService>().OfType<JetStreamConsumerHostedService>().Single();

            hosted.Endpoint.FilterSubjects.Should().Equal("x.content.>");
        }

        [Theory]
        [InlineData("content.*", "content.created", "x.content.content.*")]
        [InlineData("content.#", "content.created.v2", "x.content.content.>")]
        [InlineData("a.*.x", "a.y.*", "x.content.>")]
        [InlineData("kernel.*", "content.*", "x.content.kernel.*", "x.content.content.*")]
        public void OverlappingFilterSubjectsShouldBeMerged(string route1, string route2, params string[] expected)
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddEventBus()
                .AddJetStream(js => js
                    .AddConnection("nats", o => { })
                    .AddConsumer("a", "X_CONTENT", "durable", "nats", c => c
                        .Destination("x.content")
                        .Subscribe<ContentPublishedIntegrationEvent, ProbeHandler>(route1)
                        .Subscribe<ContentPublishedIntegrationEvent, ProbeHandler>(route2)));

            using var sp = services.BuildServiceProvider();
            var hosted = sp.GetServices<IHostedService>().OfType<JetStreamConsumerHostedService>().Single();

            hosted.Endpoint.FilterSubjects.Should().Equal(expected);
        }

        [Theory]
        [InlineData("a.*", "a.b", true)]
        [InlineData("a.>", "a.b.c", true)]
        [InlineData("a.>", "a.*", true)]
        [InlineData("a.*", "a.>", false)]
        [InlineData("a.b", "a.*", false)]
        [InlineData("a.*", "a.b.c", false)]
        [InlineData("a.*.c", "a.b.*", false)]
        public void CoversShouldFollowSubjectMatching(string cover, string filter, bool expected)
        {
            JetStreamConsumerBuilder.Covers(cover, filter).Should().Be(expected);
        }

        [Theory]
        [InlineData(true, "nats://from-config:4222")]
        [InlineData(false, "nats://from-code:4222")]
        public void LastConnectionRegistrationShouldWin(bool configLast, string expectedUrl)
        {
            var section = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?> { ["Nats:Url"] = "nats://from-config:4222" })
                .Build()
                .GetSection("Nats");
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddEventBus()
                .AddJetStream(js =>
                {
                    if (configLast)
                    {
                        js.AddConnection("nats", o => o.Url = "nats://from-code:4222").AddConnection("nats", section);
                    }
                    else
                    {
                        js.AddConnection("nats", section).AddConnection("nats", o => o.Url = "nats://from-code:4222");
                    }
                });

            using var sp = services.BuildServiceProvider();
            // NatsConnection connects lazily, so reading its options does not need a server
            sp.GetRequiredKeyedService<IJetStreamConnection>("nats").Connection.Opts.Url.Should().Be(expectedUrl);
        }

        [Fact]
        public void InvalidDefaultDestinationShouldThrow()
        {
            var services = new ServiceCollection();
            var act = () => services.AddEventBus()
                .AddJetStream(js => js.AddProducer("nats", "nats", p => p.DefaultDestination("bad dest")));

            act.Should().Throw<ArgumentException>();
        }
    }
}
