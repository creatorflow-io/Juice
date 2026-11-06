using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Juice.Messaging;
using Juice.Messaging.Outbox;
using Juice.Messaging.Outbox.Delivery;
using Juice.Messaging.Outbox.EF;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Juice.Integrations.Tests
{
    /// <summary>
    /// The batch claim on real databases: telling a worker's rows apart relies on how each provider stores time.
    /// SQL Server and PostgreSQL run when <c>ConnectionStrings__SqlServerConnection</c> and
    /// <c>ConnectionStrings__PostgreConnection</c> are set, each in a throw-away database.
    /// </summary>
    public class OutboxClaimTest
    {
        public static TheoryData<string> Providers => new() { "Sqlite", "SqlServer", "PostgreSQL" };

        [Theory(DisplayName = "A claim returns what it claimed, once")]
        [MemberData(nameof(Providers))]
        public async Task A_claim_returns_what_it_claimedAsync(string provider)
        {
            await using var database = await TestDatabase.CreateAsync(provider);
            var ids = await database.AddAsync(5);
            using var scope = database.Services.CreateScope();
            var repository = scope.ServiceProvider.GetRequiredService<IOutboxRepository<ClaimContext>>();

            var claimed = await repository.MarkAsInProgressAsync(ids);
            var again = await repository.MarkAsInProgressAsync(ids);

            claimed.Should().BeEquivalentTo(ids);
            again.Should().BeEmpty();
            (await database.StatesAsync()).Should().AllSatisfy(d =>
            {
                d.State.Should().Be(DeliveryState.InProgress);
                d.ProcessedOn.Should().NotBeNull();
            });
        }

        [Theory(DisplayName = "A claim leaves out rows that aren't waiting, and counts a retry")]
        [MemberData(nameof(Providers))]
        public async Task A_claim_takes_only_what_is_waitingAsync(string provider)
        {
            await using var database = await TestDatabase.CreateAsync(provider);
            var ids = await database.AddAsync(4);
            using var scope = database.Services.CreateScope();
            var repository = scope.ServiceProvider.GetRequiredService<IOutboxRepository<ClaimContext>>();
            await repository.MarkAsPublishedAsync(ids[0]);
            await repository.MarkAsFailedAsync(ids[1], "refused", DateTimeOffset.UtcNow);
            await repository.MarkAsInProgressAsync(ids[2]);

            var claimed = await repository.MarkAsInProgressAsync(ids);

            claimed.Should().BeEquivalentTo(new[] { ids[1], ids[3] });
            var rows = (await database.StatesAsync()).ToDictionary(d => d.DeliveryId);
            rows[ids[0]].State.Should().Be(DeliveryState.Published);
            rows[ids[1]].RetryCount.Should().Be(1, "claiming a failed delivery is a retry");
            rows[ids[3]].RetryCount.Should().Be(0);
        }

        [Theory(DisplayName = "Two workers claiming the same rows never both get one")]
        [MemberData(nameof(Providers))]
        public async Task Workers_never_share_a_rowAsync(string provider)
        {
            await using var database = await TestDatabase.CreateAsync(provider);
            var ids = await database.AddAsync(200);

            var claims = await Task.WhenAll(Enumerable.Range(0, 2).Select(worker => Task.Run(async () =>
            {
                var mine = new List<Guid>();
                // Overlapping lists, as two workers retrieving the same oldest rows have.
                foreach (var batch in ids.Chunk(20))
                {
                    using var scope = database.Services.CreateScope();
                    var repository = scope.ServiceProvider.GetRequiredService<IOutboxRepository<ClaimContext>>();
                    mine.AddRange(await repository.MarkAsInProgressAsync(batch));
                }
                return mine;
            })));

            var all = claims.SelectMany(c => c).ToArray();
            all.Should().OnlyHaveUniqueItems("a row claimed by both would be sent twice");
            all.Should().BeEquivalentTo(ids);
        }

        [Theory(DisplayName = "A batch larger than one statement takes is handled")]
        [MemberData(nameof(Providers))]
        public async Task A_large_batch_is_handledAsync(string provider)
        {
            await using var database = await TestDatabase.CreateAsync(provider);
            var ids = await database.AddAsync(1001);
            using var scope = database.Services.CreateScope();
            var repository = scope.ServiceProvider.GetRequiredService<IOutboxRepository<ClaimContext>>();

            (await repository.MarkAsInProgressAsync(ids)).Should().HaveCount(1001);
            await repository.MarkAsPublishedAsync((IReadOnlyCollection<Guid>)ids);

            (await database.StatesAsync()).Should().OnlyContain(d => d.State == DeliveryState.Published);
        }

        [Theory(DisplayName = "A deferred delivery can be claimed again, without using a retry")]
        [MemberData(nameof(Providers))]
        public async Task Deferring_uses_no_retryAsync(string provider)
        {
            await using var database = await TestDatabase.CreateAsync(provider);
            var ids = await database.AddAsync(2);
            using var scope = database.Services.CreateScope();
            var repository = scope.ServiceProvider.GetRequiredService<IOutboxRepository<ClaimContext>>();
            var retried = ids[0];
            await repository.MarkAsFailedAsync(retried, "refused", DateTimeOffset.UtcNow);
            await repository.MarkAsInProgressAsync(ids);

            await repository.DeferAsync(retried, "busy", DateTimeOffset.UtcNow);
            await repository.DeferAsync(ids[1], "busy", DateTimeOffset.UtcNow);
            var rows = (await database.StatesAsync()).ToDictionary(d => d.DeliveryId);
            rows[retried].State.Should().Be(DeliveryState.NotPublished);
            rows[retried].LastError.Should().Be("busy");
            rows[retried].NextAttemptOn.Should().NotBeNull();

            (await repository.MarkAsInProgressAsync(ids)).Should().BeEquivalentTo(ids);

            rows = (await database.StatesAsync()).ToDictionary(d => d.DeliveryId);
            rows[retried].RetryCount.Should().Be(1, "only the retry it already had");
            rows[ids[1]].RetryCount.Should().Be(0);
        }

        [Theory(DisplayName = "Deferring leaves a closed delivery closed")]
        [MemberData(nameof(Providers))]
        public async Task Deferring_doesnt_reopen_a_closed_deliveryAsync(string provider)
        {
            await using var database = await TestDatabase.CreateAsync(provider);
            var ids = await database.AddAsync(1);
            using var scope = database.Services.CreateScope();
            var repository = scope.ServiceProvider.GetRequiredService<IOutboxRepository<ClaimContext>>();
            await repository.MarkAsInProgressAsync(ids[0]);
            await repository.MarkAsPublishedAsync(ids[0]);

            await repository.DeferAsync(ids[0], "busy", DateTimeOffset.UtcNow);

            (await database.StatesAsync()).Single().State.Should().Be(DeliveryState.Published);
        }

        // Only these two: the query orders by a DateTimeOffset, which EF Core can't do on SQLite
        [Theory(DisplayName = "send-pending skips a deferred delivery until it's due")]
        [InlineData("SqlServer")]
        [InlineData("PostgreSQL")]
        public async Task Send_pending_waits_for_a_deferred_deliveryAsync(string provider)
        {
            await using var database = await TestDatabase.CreateAsync(provider);
            var ids = await database.AddAsync(3);
            using var scope = database.Services.CreateScope();
            var repository = scope.ServiceProvider.GetRequiredService<IOutboxRepository<ClaimContext>>();
            var intent = scope.ServiceProvider.GetRequiredKeyedService<IDeliveryIntent<ClaimContext>>(DeliveryIntents.SendPending);
            var (later, due, untouched) = (ids[0], ids[1], ids[2]);
            await repository.MarkAsInProgressAsync([later, due]);
            await repository.DeferAsync(later, "busy", DateTimeOffset.UtcNow.AddHours(1));
            await repository.DeferAsync(due, "busy", DateTimeOffset.UtcNow.AddSeconds(-1));

            var retrieved = await intent.RetrieveDeliveriesAsync("test", DeliveryPolicy.Default, default);

            retrieved.Select(d => d.DeliveryId).Should().BeEquivalentTo(new[] { due, untouched });
        }

        public sealed class ClaimContext(DbContextOptions<ClaimContext> options) : DbContext(options), IOutboxContext
        {
            public DbSet<OutboxEvent> Outbox { get; set; } = default!;
            public DbSet<OutboxDelivery> OutboxDeliveries { get; set; } = default!;

            protected override void OnModelCreating(ModelBuilder modelBuilder)
            {
                this.ConfigureOutbox(modelBuilder);
                if (!Database.IsSqlServer())
                {
                    // ConfigureOutbox writes its index filters in SQL Server syntax. They only speed up the retrieval.
                    foreach (var index in modelBuilder.Model.GetEntityTypes().SelectMany(e => e.GetIndexes()))
                    {
                        index.SetFilter(null);
                    }
                }
            }
        }

        private sealed class TestDatabase : IAsyncDisposable
        {
            private readonly string? _sqliteFile;
            public ServiceProvider Services { get; }

            private TestDatabase(ServiceProvider services, string? sqliteFile)
            {
                Services = services;
                _sqliteFile = sqliteFile;
            }

            public static async Task<TestDatabase> CreateAsync(string provider)
            {
                var name = $"juice_claim_{Guid.NewGuid():N}";
                var services = new ServiceCollection();
                services.AddLogging();
                string? file = null;
                switch (provider)
                {
                    case "Sqlite":
                        file = Path.Combine(Path.GetTempPath(), name + ".db");
                        services.AddDbContext<ClaimContext>(o => o.UseSqlite($"Data Source={file};Pooling=False"));
                        break;
                    case "SqlServer":
                        var sql = Environment.GetEnvironmentVariable("ConnectionStrings__SqlServerConnection");
                        if (string.IsNullOrEmpty(sql)) { Assert.Skip("ConnectionStrings__SqlServerConnection is not set"); }
                        var sqlBuilder = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(sql) { InitialCatalog = name };
                        services.AddDbContext<ClaimContext>(o => o.UseSqlServer(sqlBuilder.ConnectionString));
                        break;
                    default:
                        var pg = Environment.GetEnvironmentVariable("ConnectionStrings__PostgreConnection");
                        if (string.IsNullOrEmpty(pg)) { Assert.Skip("ConnectionStrings__PostgreConnection is not set"); }
                        var pgBuilder = new Npgsql.NpgsqlConnectionStringBuilder(pg) { Database = name };
                        services.AddDbContext<ClaimContext>(o => o.UseNpgsql(pgBuilder.ConnectionString));
                        break;
                }
                services.AddMessaging().AddOutbox();
                var provided = services.BuildServiceProvider();
                using var scope = provided.CreateScope();
                await scope.ServiceProvider.GetRequiredService<ClaimContext>().Database.EnsureCreatedAsync();
                return new TestDatabase(provided, file);
            }

            public async Task<Guid[]> AddAsync(int count)
            {
                using var scope = Services.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<ClaimContext>();
                var deliveries = Enumerable.Range(0, count).Select(_ =>
                {
                    var eventId = Guid.NewGuid();
                    return new OutboxEvent
                    {
                        EventId = eventId,
                        EventTypeName = "Thing",
                        CreationTime = DateTimeOffset.UtcNow,
                        PayloadBytes = [1],
                        Deliveries =
                        [
                            new OutboxDelivery
                            {
                                DeliveryId = Guid.NewGuid(),
                                EventId = eventId,
                                PublisherKey = "test",
                                Destination = "x",
                                CreationTime = DateTimeOffset.UtcNow,
                            }
                        ]
                    };
                }).ToArray();
                context.Outbox.AddRange(deliveries);
                await context.SaveChangesAsync();
                return deliveries.Select(e => e.Deliveries.Single().DeliveryId).ToArray();
            }

            public async Task<List<OutboxDelivery>> StatesAsync()
            {
                using var scope = Services.CreateScope();
                return await scope.ServiceProvider.GetRequiredService<ClaimContext>().OutboxDeliveries.AsNoTracking().ToListAsync();
            }

            public async ValueTask DisposeAsync()
            {
                using (var scope = Services.CreateScope())
                {
                    await scope.ServiceProvider.GetRequiredService<ClaimContext>().Database.EnsureDeletedAsync();
                }
                await Services.DisposeAsync();
                SqliteConnection.ClearAllPools();
            }
        }
    }
}
