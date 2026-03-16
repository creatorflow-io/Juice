using System.Threading.Channels;
using FluentAssertions;
using Juice.Domain;
using Juice.Messaging;
using Juice.Messaging.Local;
using Juice.Messaging.Outbox;
using Juice.Messaging.Policies;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Juice.XUnit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Xunit.Abstractions;

namespace Juice.Messaging.Local.Tests
{
    /// <summary>
    /// Tests for transaction-aware behavior of <c>IMessageService&lt;TContext&gt;</c>.
    /// Verifies that <c>PublishAsync</c> defers outbox save when inside a managed
    /// <c>TransactionBehavior</c> scope, and saves immediately when outside.
    /// </summary>
    public class TransactionAwareTests
    {
        private readonly ITestOutputHelper _output;

        public TransactionAwareTests(ITestOutputHelper output)
        {
            _output = output;
        }

        private IServiceProvider BuildServices(
            string publisherKey,
            IUnitOfWork? unitOfWork = null,
            Action<IServiceCollection>? configure = null)
        {
            var services = new ServiceCollection();
            services.AddLogging(b => b.AddTestOutputLogger(_output));

            var messaging = services.AddMessaging();
            messaging.AddLocalChannel();
            messaging.AddIdempotencyInMemory();

            services.AddSingleton<IMessagePublishingPolicy>(
                new FixedRoutePolicy(publisherKey, string.Empty));

            services.AddMediatR();

            // Register a tracking outbox service to verify AddEventAsync / SaveEventsAsync calls
            var tracker = new OutboxTracker();
            services.AddSingleton(tracker);
            services.AddScoped<IOutboxService<FakeDbContext>>(sp =>
                new TrackingOutboxService(sp.GetRequiredService<OutboxTracker>()));

            // Register the fake context (with or without IUnitOfWork)
            if (unitOfWork != null)
            {
                services.AddScoped<FakeDbContext>(_ => new ManagedFakeDbContext(unitOfWork));
            }
            else
            {
                services.AddScoped<FakeDbContext>();
            }

            // Register IMessageService<FakeDbContext>
            messaging.AddMessageService<FakeDbContext>();

            configure?.Invoke(services);
            return services.BuildServiceProvider();
        }

        // ─────────────────────────────────────────────────────────────────────
        // US1: Inside managed transaction — defer save
        // ─────────────────────────────────────────────────────────────────────

        [IgnoreOnCIFact]
        [InitializeMessageContext]
        public async Task PublishAsync_InsideManagedTransaction_DefersToTransactionBehaviorAsync()
        {
            var uow = new FakeUnitOfWork { IsManaged = true };
            var provider = BuildServices("local", uow);

            var scope = provider.CreateScope();
            var svc = scope.ServiceProvider.GetRequiredService<IMessageService<FakeDbContext>>();
            var tracker = provider.GetRequiredService<OutboxTracker>();

            await svc.PublishAsync(new TestIntegrationEvent());

            tracker.AddEventCalled.Should().BeTrue("AddEventAsync should be called to stage the event");
            tracker.SaveEventsCalled.Should().BeFalse("SaveEventsAsync should NOT be called — deferred to TransactionBehavior");
        }

        [IgnoreOnCIFact]
        [InitializeMessageContext]
        public async Task PublishAsync_InsideManagedTransaction_DefersChannelEnqueueToPostCommitAsync()
        {
            var uow = new FakeUnitOfWork { IsManaged = true };
            var provider = BuildServices("local", uow);

            var scope = provider.CreateScope();
            var svc = scope.ServiceProvider.GetRequiredService<IMessageService<FakeDbContext>>();
            var channel = provider.GetRequiredService<ChannelReader<Juice.Messaging.Local.Internal.ChannelEnvelope>>();
            var postCommit = scope.ServiceProvider.GetRequiredService<IPostCommitActions>();

            await svc.PublishAsync(new TestIntegrationEvent());

            // Before flush — not enqueued yet (data not committed)
            channel.TryRead(out _).Should().BeFalse(
                "local route should NOT enqueue to channel before post-commit flush");

            // Simulate TransactionBehavior calling Flush() after commit
            postCommit.Flush();

            // After flush — enqueued for immediate dispatch
            channel.TryRead(out _).Should().BeTrue(
                "local route should be enqueued to channel after post-commit flush");
        }

        // ─────────────────────────────────────────────────────────────────────
        // US2: Outside transaction — save immediately
        // ─────────────────────────────────────────────────────────────────────

        [IgnoreOnCIFact]
        [InitializeMessageContext]
        public async Task PublishAsync_OutsideTransaction_SavesImmediatelyAsync()
        {
            var uow = new FakeUnitOfWork { IsManaged = false };
            var provider = BuildServices("local", uow);

            var scope = provider.CreateScope();
            var svc = scope.ServiceProvider.GetRequiredService<IMessageService<FakeDbContext>>();
            var tracker = provider.GetRequiredService<OutboxTracker>();

            await svc.PublishAsync(new TestIntegrationEvent());

            tracker.AddEventCalled.Should().BeTrue("AddEventAsync should be called");
            tracker.SaveEventsCalled.Should().BeTrue("SaveEventsAsync should be called immediately outside transaction");
        }

        [IgnoreOnCIFact]
        [InitializeMessageContext]
        public async Task PublishAsync_OutsideTransaction_EnqueuesLocalChannelAsync()
        {
            var uow = new FakeUnitOfWork { IsManaged = false };
            var provider = BuildServices("local", uow);

            var scope = provider.CreateScope();
            var svc = scope.ServiceProvider.GetRequiredService<IMessageService<FakeDbContext>>();
            var channel = provider.GetRequiredService<ChannelReader<Juice.Messaging.Local.Internal.ChannelEnvelope>>();

            await svc.PublishAsync(new TestIntegrationEvent());

            channel.TryRead(out _).Should().BeTrue(
                "local route should enqueue to channel outside transaction for immediate dispatch");
        }

        [IgnoreOnCIFact]
        [InitializeMessageContext]
        public async Task PublishAsync_NullContext_SavesImmediatelyAsync()
        {
            // Build without registering FakeDbContext — _context will be null
            var services = new ServiceCollection();
            services.AddLogging(b => b.AddTestOutputLogger(_output));

            var messaging = services.AddMessaging();
            messaging.AddLocalChannel();
            messaging.AddIdempotencyInMemory();

            services.AddSingleton<IMessagePublishingPolicy>(
                new FixedRoutePolicy("local", string.Empty));
            services.AddMediatR();

            var tracker = new OutboxTracker();
            services.AddSingleton(tracker);
            services.AddScoped<IOutboxService<FakeDbContext>>(sp =>
                new TrackingOutboxService(sp.GetRequiredService<OutboxTracker>()));

            // Do NOT register FakeDbContext — so _context is null
            messaging.AddMessageService<FakeDbContext>();

            var provider = services.BuildServiceProvider();
            var scope = provider.CreateScope();
            var svc = scope.ServiceProvider.GetRequiredService<IMessageService<FakeDbContext>>();

            await svc.PublishAsync(new TestIntegrationEvent());

            tracker.SaveEventsCalled.Should().BeTrue(
                "null context should fall back to immediate save (defensive)");
        }

        // ─────────────────────────────────────────────────────────────────────
        // US3: Local-channel unaffected by transaction state
        // ─────────────────────────────────────────────────────────────────────

        [IgnoreOnCIFact]
        [InitializeMessageContext]
        public async Task PublishAsync_LocalChannel_InsideTransaction_EnqueuesImmediatelyAsync()
        {
            var uow = new FakeUnitOfWork { IsManaged = true };
            var provider = BuildServices("local-channel", uow);

            var scope = provider.CreateScope();
            var svc = scope.ServiceProvider.GetRequiredService<IMessageService<FakeDbContext>>();
            var channel = provider.GetRequiredService<ChannelReader<Juice.Messaging.Local.Internal.ChannelEnvelope>>();

            await svc.PublishAsync(new TestIntegrationEvent());

            channel.TryRead(out _).Should().BeTrue(
                "local-channel route should enqueue immediately regardless of transaction state");
        }

        [IgnoreOnCIFact]
        [InitializeMessageContext]
        public async Task PublishAsync_DualRoute_InsideTransaction_LocalWinsOverLocalChannelAsync()
        {
            var uow = new FakeUnitOfWork { IsManaged = true };

            var services = new ServiceCollection();
            services.AddLogging(b => b.AddTestOutputLogger(_output));

            var messaging = services.AddMessaging();
            messaging.AddLocalChannel();
            messaging.AddIdempotencyInMemory();

            // Policy returns both "local-channel" and "local" routes
            services.AddSingleton<IMessagePublishingPolicy>(
                new DualRoutePolicy());

            services.AddMediatR();

            var tracker = new OutboxTracker();
            services.AddSingleton(tracker);
            services.AddScoped<IOutboxService<FakeDbContext>>(sp =>
                new TrackingOutboxService(sp.GetRequiredService<OutboxTracker>()));
            services.AddScoped<FakeDbContext>(_ => new ManagedFakeDbContext(uow));
            messaging.AddMessageService<FakeDbContext>();

            var provider = services.BuildServiceProvider();
            var scope = provider.CreateScope();
            var svc = scope.ServiceProvider.GetRequiredService<IMessageService<FakeDbContext>>();
            var channel = provider.GetRequiredService<ChannelReader<Juice.Messaging.Local.Internal.ChannelEnvelope>>();

            var postCommit = scope.ServiceProvider.GetRequiredService<IPostCommitActions>();

            await svc.PublishAsync(new TestIntegrationEvent());

            // "local" supersedes "local-channel" — no channel enqueue before commit
            channel.TryRead(out _).Should().BeFalse(
                "local-channel should be suppressed when local route is present — local wins as durable superset");

            // local portion: staged but NOT saved (deferred inside managed tx)
            tracker.AddEventCalled.Should().BeTrue("outbox event should be staged");
            tracker.SaveEventsCalled.Should().BeFalse("outbox save should be deferred inside transaction");

            // After post-commit flush — local route enqueues to channel
            postCommit.Flush();
            channel.TryRead(out _).Should().BeTrue(
                "local route should be enqueued to channel after post-commit flush");
        }

        // ─── Supporting types ────────────────────────────────────────────────

        private sealed record TestIntegrationEvent : IntegrationEvent;

        private class FakeDbContext { }

        private sealed class ManagedFakeDbContext : FakeDbContext, IUnitOfWork
        {
            private readonly bool _isManaged;
            public ManagedFakeDbContext(IUnitOfWork uow) => _isManaged = uow.IsManaged;
            public bool IsManaged => _isManaged;
            public bool HasActiveTransaction => false;
            public void BeginManage() { }
            public Task<bool> CommitTransactionAsync(Guid transactionId, CancellationToken token = default)
                => Task.FromResult(true);
            public Task<int> SaveChangesAsync(CancellationToken token = default) => Task.FromResult(0);
            public Task AddAsync<T>(T entity, CancellationToken token = default) where T : class
                => Task.CompletedTask;
            public Task AddRangeAsync<T>(IEnumerable<T> entities, CancellationToken token = default) where T : class
                => Task.CompletedTask;
            public Task<IOperationResult> DeleteAsync<T>(T entity, CancellationToken token = default) where T : class
                => Task.FromResult(OperationResult.Success);
            public Task<T?> FindAsync<T>(System.Linq.Expressions.Expression<Func<T, bool>> predicate, CancellationToken token = default) where T : class
                => Task.FromResult<T?>(null);
            public IQueryable<T> Query<T>() where T : class => throw new NotImplementedException();
        }

        private sealed class FakeUnitOfWork : IUnitOfWork
        {
            public bool IsManaged { get; set; }
            public bool HasActiveTransaction => false;
            public void BeginManage() { IsManaged = true; }
            public Task<bool> CommitTransactionAsync(Guid transactionId, CancellationToken token = default)
                => Task.FromResult(true);
            public Task<int> SaveChangesAsync(CancellationToken token = default) => Task.FromResult(0);
            public Task AddAsync<T>(T entity, CancellationToken token = default) where T : class
                => Task.CompletedTask;
            public Task AddRangeAsync<T>(IEnumerable<T> entities, CancellationToken token = default) where T : class
                => Task.CompletedTask;
            public Task<IOperationResult> DeleteAsync<T>(T entity, CancellationToken token = default) where T : class
                => Task.FromResult(OperationResult.Success);
            public Task<T?> FindAsync<T>(System.Linq.Expressions.Expression<Func<T, bool>> predicate, CancellationToken token = default) where T : class
                => Task.FromResult<T?>(null);
            public IQueryable<T> Query<T>() where T : class => throw new NotImplementedException();
        }

        private sealed class OutboxTracker
        {
            public bool AddEventCalled { get; set; }
            public bool SaveEventsCalled { get; set; }
        }

        private sealed class TrackingOutboxService : IOutboxService<FakeDbContext>
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

        private sealed class FixedRoutePolicy(string publisherKey, string destination)
            : IMessagePublishingPolicy
        {
            public ValueTask<IReadOnlyCollection<PublishRoute>> ResolveAsync(PolicyResolveContext context)
            {
                IReadOnlyCollection<PublishRoute> routes = [new PublishRoute(publisherKey, destination)];
                return ValueTask.FromResult(routes);
            }
        }

        private sealed class DualRoutePolicy : IMessagePublishingPolicy
        {
            public ValueTask<IReadOnlyCollection<PublishRoute>> ResolveAsync(PolicyResolveContext context)
            {
                IReadOnlyCollection<PublishRoute> routes =
                [
                    new PublishRoute("local-channel", string.Empty),
                    new PublishRoute("local", string.Empty)
                ];
                return ValueTask.FromResult(routes);
            }
        }
    }
}
