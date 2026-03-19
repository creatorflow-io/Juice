using FluentAssertions;
using Juice.Messaging;
using Juice.Messaging.Local;
using Juice.Messaging.Local.Internal;
using Juice.Messaging.Outbox;
using Juice.Messaging.Outbox.EF;
using Juice.Messaging.Policies;
using Juice.XUnit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Threading.Channels;
using Xunit.Abstractions;

namespace Juice.Messaging.Local.Tests
{
    /// <summary>
    /// Tests for <c>AddDefaultMessageService()</c> — verifying that
    /// <see cref="IMessageService"/> is backed by <c>MessageService&lt;DefaultOutboxContext&gt;</c>
    /// for full-route publishing outside domain transactions.
    /// Covers US1 (full-route IMessageService), US2 (coexistence), and US3 (auto-wire delivery).
    /// </summary>
    public class DefaultOutboxContextTests
    {
        private readonly ITestOutputHelper _output;

        public DefaultOutboxContextTests(ITestOutputHelper output)
        {
            _output = output;
        }

        // ─── US1 (P1): Full-route IMessageService via DefaultOutboxContext ────

        /// <summary>
        /// T005 — DI registration: IMessageService resolves as IMessageService&lt;DefaultOutboxContext&gt;.
        /// Pure DI test; no real DB connection required.
        /// </summary>
        [IgnoreOnCIFact]
        public void AddDefaultMessageService_RegistersIMessageService_BackedByDefaultOutboxContext()
        {
            var services = new ServiceCollection();
            services.AddLogging(b => b.AddTestOutputLogger(_output));
            services.AddSingleton<IMessagePublishingPolicy>(new FixedRoutePolicy("local", string.Empty));

            var messaging = services.AddMessaging();
            // No provider needed for DI resolution — DB connection is never opened in this test
            messaging.AddDefaultMessageService(opts => { });

            var provider = services.BuildServiceProvider();
            using var scope = provider.CreateScope();

            var svc = scope.ServiceProvider.GetRequiredService<IMessageService>();
            svc.Should().NotBeNull();
            svc.Should().BeAssignableTo<IMessageService<DefaultOutboxContext>>(
                "IMessageService should be backed by MessageService<DefaultOutboxContext> when AddDefaultMessageService() is called");
        }

        /// <summary>
        /// T006 — local-channel route: message dispatched to in-memory channel; no outbox write attempted.
        /// </summary>
        [IgnoreOnCIFact]
        [InitializeMessageContext]
        public async Task PublishAsync_WithLocalChannelRoute_DispatchesToChannelAsync()
        {
            var outboxTracker = new OutboxTracker();
            var provider = BuildServicesWithTracking("local-channel", outboxTracker);

            using var cts = new CancellationTokenSource();
            var hostedServices = provider.GetServices<IHostedService>().ToList();
            foreach (var hs in hostedServices) await hs.StartAsync(cts.Token);

            var channel = provider.GetRequiredService<ChannelReader<ChannelEnvelope>>();

            using var scope = provider.CreateScope();
            var svc = scope.ServiceProvider.GetRequiredService<IMessageService>();

            await svc.PublishAsync(new TestEvent());

            // local-channel route → message in channel; outbox NOT touched
            channel.TryRead(out _).Should().BeTrue("local-channel route should enqueue to in-memory channel");
            outboxTracker.AddEventCalled.Should().BeFalse("local-channel route must NOT write to outbox");
            outboxTracker.SaveEventsCalled.Should().BeFalse("local-channel route must NOT call SaveEventsAsync");

            cts.Cancel();
            foreach (var hs in hostedServices) await hs.StopAsync(CancellationToken.None);
        }

        /// <summary>
        /// T007 — local route: PublishAsync calls AddEventAsync and SaveEventsAsync on the outbox service.
        /// Uses a TrackingOutboxService to avoid requiring a real DB connection.
        /// </summary>
        [IgnoreOnCIFact]
        [InitializeMessageContext]
        public async Task PublishAsync_WithLocalRoute_CallsSaveEventsAsyncImmediatelyAsync()
        {
            var outboxTracker = new OutboxTracker();
            var provider = BuildServicesWithTracking("local", outboxTracker);

            using var scope = provider.CreateScope();
            var svc = scope.ServiceProvider.GetRequiredService<IMessageService>();

            await svc.PublishAsync(new TestEvent());

            outboxTracker.AddEventCalled.Should().BeTrue("AddEventAsync should be called for local route");
            outboxTracker.SaveEventsCalled.Should().BeTrue(
                "SaveEventsAsync should be called immediately (DefaultOutboxContext is never IsManaged)");
        }

        // ─── US2 (P2): Coexistence with domain-aware IMessageService<TContext> ─

        /// <summary>
        /// T009 — DI coexistence: both IMessageService (default outbox) and IMessageService&lt;FakeDbContext&gt;
        /// (domain-aware) resolve independently without DI conflict.
        /// </summary>
        [IgnoreOnCIFact]
        public void AddDefaultMessageService_And_AddMessageServiceTContext_CoexistWithoutConflict()
        {
            var services = new ServiceCollection();
            services.AddLogging(b => b.AddTestOutputLogger(_output));
            services.AddSingleton<IMessagePublishingPolicy>(new FixedRoutePolicy("local", string.Empty));

            var messaging = services.AddMessaging();

            // Domain-aware: used inside TransactionBehavior
            messaging.AddMessageService<FakeDbContext>();
            services.AddScoped<FakeDbContext>();
            services.AddScoped<IOutboxService<FakeDbContext>>(_ => new FakeOutboxService());

            // Default outbox: used outside transactions
            messaging.AddDefaultMessageService(opts => { });

            var provider = services.BuildServiceProvider();
            using var scope = provider.CreateScope();

            var defaultSvc = scope.ServiceProvider.GetRequiredService<IMessageService>();
            var domainSvc = scope.ServiceProvider.GetRequiredService<IMessageService<FakeDbContext>>();

            defaultSvc.Should().NotBeNull();
            domainSvc.Should().NotBeNull();
            defaultSvc.Should().BeAssignableTo<IMessageService<DefaultOutboxContext>>(
                "IMessageService should be backed by DefaultOutboxContext");
            defaultSvc.Should().NotBeSameAs(domainSvc,
                "each service writes to its own outbox context — they must be independent");
        }

        // ─── Helpers ──────────────────────────────────────────────────────────

        /// <summary>
        /// Builds services using <c>AddDefaultMessageService()</c> with a <see cref="TrackingOutboxService"/>
        /// overriding the real outbox service, so tests can verify routing without a live DB.
        /// The closed-generic registration takes precedence over the open-generic fallback.
        /// </summary>
        private IServiceProvider BuildServicesWithTracking(string publisherKey, OutboxTracker tracker)
        {
            var services = new ServiceCollection();
            services.AddLogging(b => b.AddTestOutputLogger(_output));
            services.AddMediatR();
            services.AddSingleton<IMessagePublishingPolicy>(new FixedRoutePolicy(publisherKey, string.Empty));

            var messaging = services.AddMessaging();
            messaging.AddLocalChannel();
            messaging.AddIdempotencyInMemory();
            messaging.AddDefaultMessageService(opts => { });

            // Override open-generic IOutboxService<> with a closed-generic tracking service.
            // The closed-generic registration takes precedence over the open-generic fallback.
            services.AddSingleton(tracker);
            services.AddScoped<IOutboxService<DefaultOutboxContext>>(sp =>
                new TrackingOutboxService(sp.GetRequiredService<OutboxTracker>()));

            return services.BuildServiceProvider();
        }

        // ─── Supporting types ─────────────────────────────────────────────────

        private sealed record TestEvent : IntegrationEvent;

        private class FakeDbContext { }

        private sealed class OutboxTracker
        {
            public bool AddEventCalled { get; set; }
            public bool SaveEventsCalled { get; set; }
        }

        private sealed class TrackingOutboxService : IOutboxService<DefaultOutboxContext>
        {
            private readonly OutboxTracker _tracker;
            public TrackingOutboxService(OutboxTracker tracker) => _tracker = tracker;

            public ValueTask AddEventAsync(IMessage message)
            {
                _tracker.AddEventCalled = true;
                return ValueTask.CompletedTask;
            }

            public ValueTask SaveEventsAsync(Guid? transactionId, CancellationToken cancellationToken = default)
            {
                _tracker.SaveEventsCalled = true;
                return ValueTask.CompletedTask;
            }
        }

        private sealed class FakeOutboxService : IOutboxService<FakeDbContext>
        {
            public ValueTask AddEventAsync(IMessage message) => ValueTask.CompletedTask;
            public ValueTask SaveEventsAsync(Guid? transactionId, CancellationToken cancellationToken = default)
                => ValueTask.CompletedTask;
        }

        private sealed class FixedRoutePolicy(string publisherKey, string destination)
            : IMessagePublishingPolicy
        {
            public ValueTask<IReadOnlyCollection<PublishRoute>> ResolveAsync(PolicyResolveContext context)
            {
                IReadOnlyCollection<PublishRoute> routes = [new PublishRoute(publisherKey, destination)];
                return ValueTask.FromResult(routes);
            }
        }
    }
}
