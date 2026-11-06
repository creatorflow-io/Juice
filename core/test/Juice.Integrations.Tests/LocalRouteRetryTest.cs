using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Juice.Messaging;
using Juice.Messaging.Idempotency;
using Juice.Messaging.Outbox;
using Juice.Messaging.Outbox.Delivery;
using Juice.Messaging.Outbox.Delivery.Processing;
using Juice.Messaging.Outbox.EF;
using Juice.Messaging.Policies;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Juice.Integrations.Tests
{
    /// <summary>
    /// A message on the "local" route, published in a managed transaction, is dispatched right after the commit and is
    /// also delivered from the outbox. The second must not close the row while the first can still fail.
    /// Everything is real except the transaction itself, whose steps are run by hand in the order TransactionBehavior uses.
    /// </summary>
    public sealed class LocalRouteRetryTest : IAsyncLifetime
    {
        private static readonly TimeSpan Wait = TimeSpan.FromSeconds(10);
        private static readonly TimeSpan RetryDelay = TimeSpan.FromMilliseconds(100);

        private readonly string _file = Path.Combine(Path.GetTempPath(), $"juice_local_{Guid.NewGuid():N}.db");
        private readonly HandlerProbe _probe = new();
        private ServiceProvider _services = default!;
        private List<IHostedService> _hosted = default!;
        private bool _channelClosesRows;

        public async ValueTask InitializeAsync()
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddMediatR();
            services.AddDbContext<RouteContext>(o => o.UseSqlite($"Data Source={_file};Pooling=False"));
            var messaging = services.AddMessaging();
            messaging.AddOutbox();
            messaging.AddLocalChannel();
            messaging.AddLocalPublisher();
            services.AddDistributedMemoryCache();
            messaging.AddIdempotencyDistributedCache(); // shared across scopes, as Redis and the database are
            messaging.AddMessageService<RouteContext>();
            services.AddSingleton<IMessagePublishingPolicy>(new LocalRoutePolicy());
            // The channel only closes rows when an application registers this; AddOutbox registers the generic one alone.
            services.AddScoped<IOutboxRepository>(sp => _channelClosesRows ? sp.GetRequiredService<IOutboxRepository<RouteContext>>() : null!);
            services.AddSingleton(_probe);
            services.AddTransient<IIntegrationEventHandler<OrderPlaced>, OrderPlacedHandler>();
            services.AddKeyedScoped<IDeliveryIntent<RouteContext>>("send-pending", (sp, _) => new WaitingRows(sp.GetRequiredService<RouteContext>()));
            _ = new DeliveryProcessorBuilder("local", services);
            _services = services.BuildServiceProvider();

            using (var scope = _services.CreateScope())
            {
                await scope.ServiceProvider.GetRequiredService<RouteContext>().Database.EnsureCreatedAsync();
            }
            _hosted = _services.GetServices<IHostedService>().ToList();
            foreach (var hosted in _hosted)
            {
                await hosted.StartAsync(default);
            }
        }

        public async ValueTask DisposeAsync()
        {
            _probe.Gate.TrySetResult();
            foreach (var hosted in _hosted)
            {
                await hosted.StopAsync(default);
            }
            await _services.DisposeAsync();
            SqliteConnection.ClearAllPools();
            File.Delete(_file);
        }

        [Theory(DisplayName = "Control: a dispatch that failed before delivery ran is retried by delivery")]
        [InlineData(true)]
        [InlineData(false)]
        public async Task A_failure_before_delivery_is_retriedAsync(bool managed)
        {
            _probe.Gate.SetResult();
            _probe.FailFirst = true;

            await PublishAsync(managed);
            await _probe.Finished.Task.WaitAsync(Wait);
            await WaitForDispatchToSettleAsync();
            await DeliverAsync();

            _probe.Succeeded.Should().Be(1, "delivery retried the failed dispatch");
            (await StateAsync()).Should().Be(DeliveryState.Published);
        }

        [Theory(DisplayName = "A dispatch that fails while delivery runs is still retried")]
        [InlineData(true)]
        [InlineData(false)]
        public async Task A_failure_during_delivery_is_retriedAsync(bool managed)
        {
            _probe.FailFirst = true;

            await PublishAsync(managed);
            await _probe.Started.Task.WaitAsync(Wait);

            await DeliverAsync(); // sees the first dispatch in progress
            (await StateAsync()).Should().Be(DeliveryState.NotPublished, "nothing has handled the message yet");

            _probe.Gate.SetResult();
            await _probe.Finished.Task.WaitAsync(Wait);
            await WaitForDispatchToSettleAsync();
            await DeliverAsync(); // the next round would retry anything still waiting

            var state = await StateAsync();
            _probe.Attempts.Should().Be(2, $"the first attempt failed, so the message needs another (its row is {state})");
            _probe.Succeeded.Should().Be(1, "a message on a durable route isn't lost because its handler failed once");
            state.Should().Be(DeliveryState.Published);
            (await RetryCountAsync()).Should().Be(0, "waiting for another dispatch doesn't use up a retry");
        }

        [Fact(DisplayName = "A row isn't closed by a dispatch that found delivery already running")]
        public async Task The_channel_leaves_the_row_to_a_running_dispatchAsync()
        {
            _channelClosesRows = true;
            var order = new OrderPlaced();
            using var guard = _services.CreateScope();
            var idempotency = guard.ServiceProvider.GetRequiredService<IIdempotencyService>();
            (await idempotency.TryBeginRequestAsync(nameof(OrderPlaced), $"test:{order.MessageId}")).Outcome.Should().Be(IdempotencyOutcome.Created);
            _probe.Gate.SetResult();

            await PublishAsync(managed: false, order);
            await WaitForDispatchToSettleAsync();

            _probe.Attempts.Should().Be(0, "the guard is taken");
            (await StateAsync()).Should().Be(DeliveryState.NotPublished, "the dispatch holding the guard can still fail");

            await idempotency.TryCompleteRequestAsync(nameof(OrderPlaced), $"test:{order.MessageId}", false);
            await DeliverAsync();

            _probe.Succeeded.Should().Be(1);
            (await StateAsync()).Should().Be(DeliveryState.Published);
        }

        [Theory(DisplayName = "A dispatch that succeeds while delivery runs is handled once")]
        [InlineData(true)]
        [InlineData(false)]
        public async Task A_success_during_delivery_is_handled_onceAsync(bool managed)
        {
            await PublishAsync(managed);
            await _probe.Started.Task.WaitAsync(Wait);

            await DeliverAsync();

            _probe.Gate.SetResult();
            await _probe.Finished.Task.WaitAsync(Wait);
            await WaitForDispatchToSettleAsync();
            await DeliverAsync();

            _probe.Attempts.Should().Be(1);
            _probe.Succeeded.Should().Be(1);
            (await StateAsync()).Should().Be(DeliveryState.Published);
        }

        private async Task PublishAsync(bool managed, OrderPlaced? order = null)
        {
            MessageContext.Initialize(Guid.NewGuid().ToString(), null, Guid.NewGuid().ToString(), "test");
            try
            {
                using var scope = _services.CreateScope();
                var provider = scope.ServiceProvider;
                provider.GetRequiredService<RouteContext>().IsManaged = managed;
                await provider.GetRequiredService<IMessageService<RouteContext>>().PublishAsync(order ?? new OrderPlaced());
                if (managed)
                {
                    await provider.GetRequiredService<IOutboxService<RouteContext>>().SaveEventsAsync(Guid.NewGuid());
                    await provider.GetRequiredService<IPostCommitActions>().FlushAsync();
                }
            }
            finally
            {
                MessageContext.Clear();
            }
        }

        private async Task DeliverAsync()
        {
            var worker = new DeliveryWorker<RouteContext>(_services, "local", "send-pending",
                new FixedPolicy(DeliveryPolicy.Default with { BatchSize = 10, InitialRetryDelay = RetryDelay }), NullLogger<DeliveryWorker<RouteContext>>.Instance);
            await worker.InitializeAsync(default);
            await worker.ExecuteAsync(default);
        }

        /// <summary>The dispatcher closes its idempotency guard just after the handler returns, and a deferred row comes due.</summary>
        private static Task WaitForDispatchToSettleAsync() => Task.Delay(300);

        private async Task<DeliveryState> StateAsync() => (await RowAsync()).State;

        private async Task<int> RetryCountAsync() => (await RowAsync()).RetryCount;

        private async Task<OutboxDelivery> RowAsync()
        {
            using var scope = _services.CreateScope();
            var deliveries = await scope.ServiceProvider.GetRequiredService<RouteContext>().OutboxDeliveries.AsNoTracking().ToListAsync();
            return deliveries.Single();
        }

        public sealed record OrderPlaced : IntegrationEvent;

        public sealed class HandlerProbe
        {
            public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
            public TaskCompletionSource Gate { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
            public TaskCompletionSource Finished { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
            public bool FailFirst { get; set; }
            public int Attempts;
            public int Succeeded;
        }

        public sealed class OrderPlacedHandler(HandlerProbe probe) : IIntegrationEventHandler<OrderPlaced>
        {
            public async Task HandleAsync(OrderPlaced @event)
            {
                var attempt = Interlocked.Increment(ref probe.Attempts);
                probe.Started.TrySetResult();
                await probe.Gate.Task;
                try
                {
                    if (attempt == 1 && probe.FailFirst)
                    {
                        throw new InvalidOperationException("The handler failed");
                    }
                    Interlocked.Increment(ref probe.Succeeded);
                }
                finally
                {
                    probe.Finished.TrySetResult();
                }
            }
        }

        public sealed class RouteContext(DbContextOptions<RouteContext> options) : DbContext(options), IOutboxContext, IManagable
        {
            public DbSet<OutboxEvent> Outbox { get; set; } = default!;
            public DbSet<OutboxDelivery> OutboxDeliveries { get; set; } = default!;
            public bool IsManaged { get; set; }

            protected override void OnModelCreating(ModelBuilder modelBuilder)
            {
                this.ConfigureOutbox(modelBuilder);
                foreach (var index in modelBuilder.Model.GetEntityTypes().SelectMany(e => e.GetIndexes()))
                {
                    index.SetFilter(null);
                }
            }
        }

        private sealed class LocalRoutePolicy : IMessagePublishingPolicy
        {
            public ValueTask<IReadOnlyCollection<PublishRoute>> ResolveAsync(PolicyResolveContext context)
                => ValueTask.FromResult<IReadOnlyCollection<PublishRoute>>([new PublishRoute("local", string.Empty)]);
        }

        private sealed class FixedPolicy(DeliveryPolicy policy) : IDeliveryPolicyResolver
        {
            public ValueTask<DeliveryPolicy> GetPolicyAsync(DeliveryContext context, CancellationToken cancellationToken)
                => ValueTask.FromResult(policy);
        }

        /// <summary>The rows a pending or retry intent would pick up, due ones only; the real intents' queries don't run on SQLite.</summary>
        private sealed class WaitingRows(RouteContext context) : IDeliveryIntent<RouteContext>
        {
            public string Name => "send-pending";

            public async Task<IEnumerable<OutboxDelivery>> RetrieveDeliveriesAsync(string publisherKey, DeliveryPolicy policy, CancellationToken cancellationToken)
                => (await context.OutboxDeliveries.Include(d => d.OutboxEvent).AsNoTracking()
                    .Where(d => d.PublisherKey == publisherKey && (d.State == DeliveryState.NotPublished || d.State == DeliveryState.Failed))
                    .ToListAsync(cancellationToken))
                    .Where(d => d.NextAttemptOn == null || d.NextAttemptOn < DateTimeOffset.UtcNow);
        }
    }
}
