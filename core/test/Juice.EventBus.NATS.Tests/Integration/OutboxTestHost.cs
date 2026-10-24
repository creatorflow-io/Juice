using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Juice.Messaging;
using Juice.Messaging.Outbox;
using Juice.Messaging.Outbox.Delivery.Internal;
using Juice.Messaging.Outbox.EF;
using Juice.Messaging.Policies;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Juice.EventBus.NATS.Tests.Integration
{
    public class NatsOutboxContext : DbContext, IOutboxContext
    {
        public NatsOutboxContext(DbContextOptions<NatsOutboxContext> options) : base(options)
        {
        }

        public DbSet<OutboxEvent> Outbox { get; set; } = default!;
        public DbSet<OutboxDelivery> OutboxDeliveries { get; set; } = default!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            this.ConfigureOutbox(modelBuilder);
        }
    }

    /// <summary>
    /// Outbox + delivery host publishing through the JetStream producer.
    /// Uses a throw-away SQL Server database per host (see <see cref="TestSettings.NewSqlServerConnectionString"/>):
    /// the outbox delivery queries use <c>DateTimeOffset</c> comparisons and <c>ExecuteUpdate</c> that SQLite cannot translate.
    /// </summary>
    internal sealed class OutboxTestHost : IAsyncDisposable
    {
        private IHostedService[] _hostedServices = [];
        private bool _keepDatabase;
        public ServiceProvider Services { get; }

        private OutboxTestHost(ServiceProvider services)
        {
            Services = services;
        }

        public static string NewConnectionString() => TestSettings.NewSqlServerConnectionString();

        public static OutboxTestHost Create(ITestOutputHelper output,
            Func<PolicyResolveContext, IReadOnlyCollection<PublishRoute>> routes,
            Action<JetStreamEventBusBuilder>? jetStream = default,
            Action<IServiceCollection>? configure = default,
            string[]? publishers = default,
            string? connectionString = default,
            string producerKey = "nats",
            int maxRetryAttempts = 20)
        {
            var keepDatabase = connectionString != null;
            connectionString ??= NewConnectionString();
            var services = new ServiceCollection();
            services.AddLogging(builder => builder.AddTestOutputLogger(output).SetMinimumLevel(LogLevel.Information));
            services.AddDbContext<NatsOutboxContext>(o => o.UseSqlServer(connectionString));
            services.AddDistributedMemoryCache();
            services.AddSingleton<IMessagePublishingPolicy>(new DelegatePolicy(routes));

            services.AddMessaging()
                .AddOutbox()
                .AddMessageService<NatsOutboxContext>()
                .AddIdempotencyDistributedCache()
                .AddDelivery(delivery =>
                {
                    delivery.AddDeliveryPolicies(o => o.DefaultPolicy = new PolicyConfiguration
                    {
                        Interval = TimeSpan.FromMilliseconds(100),
                        BatchSize = 50,
                        Timeout = TimeSpan.FromSeconds(5),
                        InitialRetryDelay = TimeSpan.FromMilliseconds(200),
                        RetryDelayMultiplier = 1.0,
                        MaxRetryAttempts = maxRetryAttempts
                    });
                    foreach (var publisher in publishers ?? ["nats"])
                    {
                        delivery.AddDeliveryProcessor<NatsOutboxContext>(publisher);
                    }
                    delivery.EventBus.AddJetStream(js =>
                    {
                        js.AddConnection("nats", ConsumerTestHost.ConfigureConnection);
                        js.AddConnection(ConsumerTestHost.ConsumerConnection, ConsumerTestHost.ConfigureConnection);
                        js.AddProducer(producerKey, "nats");
                        jetStream?.Invoke(js);
                    });
                });
            configure?.Invoke(services);

            var sp = services.BuildServiceProvider();
            using (var scope = sp.CreateScope())
            {
                scope.ServiceProvider.GetRequiredService<NatsOutboxContext>().Database.EnsureCreated();
            }
            return new OutboxTestHost(sp) { _keepDatabase = keepDatabase };
        }

        public async Task StartAsync()
        {
            _hostedServices = Services.GetServices<IHostedService>().ToArray();
            foreach (var hostedService in _hostedServices)
            {
                await hostedService.StartAsync(CancellationToken.None);
            }
        }

        public async Task StopAsync()
        {
            foreach (var hostedService in Enumerable.Reverse(_hostedServices))
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                await hostedService.StopAsync(cts.Token);
            }
            _hostedServices = [];
        }

        public async Task PublishAsync(IMessage message)
        {
            using var scope = Services.CreateScope();
            await scope.ServiceProvider.GetRequiredService<IMessageService<NatsOutboxContext>>().PublishAsync(message);
        }

        public async Task<List<OutboxDelivery>> GetDeliveriesAsync()
        {
            using var scope = Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<NatsOutboxContext>();
            return await context.OutboxDeliveries.AsNoTracking().ToListAsync();
        }

        public async ValueTask DisposeAsync()
        {
            await StopAsync();
            if (!_keepDatabase)
            {
                using var scope = Services.CreateScope();
                await scope.ServiceProvider.GetRequiredService<NatsOutboxContext>().Database.EnsureDeletedAsync();
            }
            await Services.DisposeAsync();
        }

        private sealed class DelegatePolicy : IMessagePublishingPolicy
        {
            private readonly Func<PolicyResolveContext, IReadOnlyCollection<PublishRoute>> _routes;
            public DelegatePolicy(Func<PolicyResolveContext, IReadOnlyCollection<PublishRoute>> routes) => _routes = routes;

            public ValueTask<IReadOnlyCollection<PublishRoute>> ResolveAsync(PolicyResolveContext context)
                => ValueTask.FromResult(_routes(context));
        }
    }
}
