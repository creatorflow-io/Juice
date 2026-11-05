using System;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Juice.EF;
using Juice.EF.Extensions;
using Juice.Extensions.MultiTenant;
using Juice.MultiTenant;
using Finbuckle.MultiTenant.EntityFrameworkCore;
using Juice.Measurement;
using Juice.Measurement.Internal;
using Juice.MultiTenant.EF.Extensions;
using MultiTenantDbContext = Juice.MultiTenant.EF.MultiTenantDbContext;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Juice.Integrations.Tests
{
    /// <summary>
    /// A pooled context must come back clean, as EF Core's own reset leaves it: nothing tracked, no state of the last
    /// user. Used one at a time, as a message consumer does, every lease is the same context. Real SQLite.
    /// </summary>
    public class PooledDbContextTest : IDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly ServiceProvider _provider;
        private readonly IDbContextFactory<ThingContext> _pool;

        public PooledDbContextTest()
        {
            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();
            _provider = new ServiceCollection()
                .AddPooledDbContextFactory<ThingContext>(options => options.UseSqlite(_connection))
                .BuildServiceProvider();
            _pool = _provider.GetRequiredService<IDbContextFactory<ThingContext>>();

            var standalone = new DbContextOptionsBuilder<ThingContext>().UseSqlite(_connection).Options;
            using var context = new ThingContext(standalone);
            context.Database.EnsureCreated();
        }

        public void Dispose()
        {
            _provider.Dispose();
            _connection.Dispose();
        }

        private static async Task ReturnAsync(DbContext context, bool asynchronously)
        {
            if (asynchronously)
            {
                await context.DisposeAsync();
            }
            else
            {
                context.Dispose();
            }
        }

        private ThingContext Outside()
            => new(new DbContextOptionsBuilder<ThingContext>().UseSqlite(_connection).Options);

        [Theory(DisplayName = "The next lease is the same context, tracking nothing")]
        [InlineData(false)]
        [InlineData(true)]
        public async Task Returned_context_tracks_nothing_Async(bool asynchronously)
        {
            var first = _pool.CreateDbContext();
            first.Add(new Thing { Id = 1, Name = "a" });
            await first.SaveChangesAsync();
            await ReturnAsync(first, asynchronously);

            await using var next = _pool.CreateDbContext();
            next.Should().BeSameAs(first, "otherwise this test proves nothing about reuse");
            next.ChangeTracker.Entries().Should().BeEmpty();
        }

        [Theory(DisplayName = "A row one user added and never saved is not saved by the next user")]
        [InlineData(false)]
        [InlineData(true)]
        public async Task Abandoned_row_is_not_saved_by_the_next_user_Async(bool asynchronously)
        {
            var first = _pool.CreateDbContext();
            first.Add(new Thing { Id = 1, Name = "abandoned" });
            await ReturnAsync(first, asynchronously);

            await using (var next = _pool.CreateDbContext())
            {
                next.Add(new Thing { Id = 2, Name = "kept" });
                await next.SaveChangesAsync();
            }

            await using var check = Outside();
            (await check.Things.Select(t => t.Name).ToListAsync()).Should().Equal("kept");
        }

        [Theory(DisplayName = "A failed save does not fail the next user's unrelated save")]
        [InlineData(false)]
        [InlineData(true)]
        public async Task Failed_save_does_not_poison_the_next_user_Async(bool asynchronously)
        {
            await using (var seed = Outside())
            {
                seed.Add(new Thing { Id = 1, Name = "seed" });
                await seed.SaveChangesAsync();
            }

            var failing = _pool.CreateDbContext();
            failing.Add(new Thing { Id = 1, Name = "duplicate" });
            await failing.Invoking(c => c.SaveChangesAsync()).Should().ThrowAsync<DbUpdateException>();
            await ReturnAsync(failing, asynchronously);

            await using var next = _pool.CreateDbContext();
            next.Add(new Thing { Id = 2, Name = "good" });
            await next.Invoking(c => c.SaveChangesAsync()).Should().NotThrowAsync();
        }

        [Theory(DisplayName = "The next user reads the stored value, not the last user's copy")]
        [InlineData(false)]
        [InlineData(true)]
        public async Task Next_user_reads_the_stored_value_Async(bool asynchronously)
        {
            await using (var seed = Outside())
            {
                seed.Add(new Thing { Id = 1, Name = "old" });
                await seed.SaveChangesAsync();
            }

            var reader = _pool.CreateDbContext();
            await reader.Things.FirstAsync(t => t.Id == 1);
            await ReturnAsync(reader, asynchronously);

            await using (var outside = Outside())
            {
                outside.Update(new Thing { Id = 1, Name = "new" });
                await outside.SaveChangesAsync();
            }

            await using var next = _pool.CreateDbContext();
            (await next.Things.FindAsync(1))!.Name.Should().Be("new");
            (await next.Things.FirstAsync(t => t.Id == 1)).Name.Should().Be("new");
        }

        [Theory(DisplayName = "Nothing of the last user's unit of work is left")]
        [InlineData(false)]
        [InlineData(true)]
        public async Task Last_users_state_is_cleared_Async(bool asynchronously)
        {
            var first = _pool.CreateDbContext();
            first.BeginManage();
            first.SetUser(new System.Security.Claims.ClaimsPrincipal());
            first.Add(new Thing { Id = 1, Name = "a" });
            await first.SaveChangesAsync();
            first.PendingAuditEntries.Should().NotBeEmpty("a managed context keeps them until it commits");
            await ReturnAsync(first, asynchronously);

            await using var next = _pool.CreateDbContext();
            next.Should().BeSameAs(first);
            next.IsManaged.Should().BeFalse();
            next.UserPrincipal.Should().BeNull();
            next.PendingAuditEntries.Should().BeEmpty();
            next.PendingDataEvents.Should().BeEmpty();
        }

        [Theory(DisplayName = "A context is cleaned before it goes back: the next user can't get it half cleaned")]
        [InlineData(false)]
        [InlineData(true)]
        public async Task Context_is_cleaned_before_it_is_available_again_Async(bool asynchronously)
        {
            var first = _pool.CreateDbContext();
            ThingContext? leasedDuringCleanup = null;
            first.OnReset = () => leasedDuringCleanup = _pool.CreateDbContext();
            await ReturnAsync(first, asynchronously);

            leasedDuringCleanup.Should().NotBeNull();
            leasedDuringCleanup.Should().NotBeSameAs(first);
            leasedDuringCleanup!.OnReset = null;
            await leasedDuringCleanup.DisposeAsync();
        }

        [Theory(DisplayName = "A transaction the last user left open is not the next user's")]
        [InlineData(false)]
        [InlineData(true)]
        public async Task Abandoned_transaction_is_closed_Async(bool asynchronously)
        {
            var first = _pool.CreateDbContext();
            await first.Database.BeginTransactionAsync();
            first.Add(new Thing { Id = 1, Name = "in the abandoned transaction" });
            await first.SaveChangesAsync();
            await ReturnAsync(first, asynchronously);

            await using var next = _pool.CreateDbContext();
            next.Should().BeSameAs(first);
            next.Database.CurrentTransaction.Should().BeNull();
            next.HasActiveTransaction.Should().BeFalse();
            next.Add(new Thing { Id = 2, Name = "kept" });
            await next.SaveChangesAsync();

            await using var check = Outside();
            (await check.Things.Select(t => t.Name).ToListAsync()).Should().Equal("kept");
        }

        [Theory(DisplayName = "Configuration stays across uses, and a scoped time tracker doesn't")]
        [InlineData(false)]
        [InlineData(true)]
        public async Task Configuration_stays_and_the_time_tracker_does_not_Async(bool asynchronously)
        {
            var first = _pool.CreateDbContext();
            first.Configure("orders", new TimeTracker());
            await ReturnAsync(first, asynchronously);

            await using var next = _pool.CreateDbContext();
            next.Should().BeSameAs(first);
            next.Schema.Should().Be("orders");
            next.TimeTracker.Should().BeNull();
        }

        [Theory(DisplayName = "AddDbContextPool: the same, when a scope ends")]
        [InlineData(false)]
        [InlineData(true)]
        public async Task Scoped_pool_comes_back_clean_Async(bool asynchronously)
        {
            await using var provider = new ServiceCollection()
                .AddDbContextPool<ThingContext>(options => options.UseSqlite(_connection))
                .BuildServiceProvider();

            ThingContext first;
            if (asynchronously)
            {
                await using var scope = provider.CreateAsyncScope();
                first = Dirty(scope.ServiceProvider.GetRequiredService<ThingContext>());
            }
            else
            {
                using var scope = provider.CreateScope();
                first = Dirty(scope.ServiceProvider.GetRequiredService<ThingContext>());
            }

            await using var nextScope = provider.CreateAsyncScope();
            var next = nextScope.ServiceProvider.GetRequiredService<ThingContext>();
            next.Should().BeSameAs(first);
            next.ChangeTracker.Entries().Should().BeEmpty();
            next.Database.CurrentTransaction.Should().BeNull();
            next.IsManaged.Should().BeFalse();
            next.UserPrincipal.Should().BeNull();

            static ThingContext Dirty(ThingContext context)
            {
                context.Database.BeginTransaction();
                context.BeginManage();
                context.SetUser(new System.Security.Claims.ClaimsPrincipal());
                context.Add(new Thing { Id = 1, Name = "left behind" });
                return context;
            }
        }

        [Fact(DisplayName = "Disposing doesn't touch a context that is back in the pool")]
        public void Disposing_leaves_the_returned_context_alone()
        {
            var first = _pool.CreateDbContext();
            var resets = 0;
            first.OnReset = () => resets++;
            first.ResetsAreCounted = true;

            first.Dispose();

            resets.Should().Be(1, "the pool's call, not a second one after the context became available");
            first.ResetsAreCounted = false;
        }

        [Theory(DisplayName = "A pooled context serving another tenant doesn't hand it the last tenant's row")]
        [InlineData(false)]
        [InlineData(true)]
        public async Task Next_tenant_does_not_get_the_last_tenants_row_Async(bool asynchronously)
        {
            await using var provider = new ServiceCollection().Also(services =>
            {
                services.AddTestTenants<TenantInfo>("tenant-A", "tenant-B");
                services.AddPooledDbContextFactory<TenantThingContext>(options => options.UseSqlite(_connection));
            }).BuildServiceProvider();
            var pool = provider.GetRequiredService<IDbContextFactory<TenantThingContext>>();
            TenantThingContext Lease(IServiceProvider services)
            {
                var context = pool.CreateDbContext();
                context.ConfigureServices(services);
                return context;
            }

            TenantThingContext? first = null;
            await provider.TenantInvokeAsync(async http =>
            {
                first = Lease(http.RequestServices);
                await first.Database.ExecuteSqlRawAsync(
                    "CREATE TABLE TenantThings (Id INTEGER NOT NULL PRIMARY KEY, Name TEXT NOT NULL, TenantId TEXT)");
                first.Add(new TenantThing { Id = 1, Name = "tenant-A's secret" });
                await first.SaveChangesAsync();
                await ReturnAsync(first, asynchronously);
            }, "tenant-A");

            await provider.TenantInvokeAsync(async http =>
            {
                await using var next = Lease(http.RequestServices);
                next.Should().BeSameAs(first);
                next.TenantInfo!.Id.Should().Be("tenant-B");
                (await next.Things.FindAsync(1)).Should().BeNull();
                next.ChangeTracker.Entries().Should().BeEmpty();
                (await next.Things.ToListAsync()).Should().BeEmpty();
            }, "tenant-B");
        }

        public class Thing
        {
            public int Id { get; set; }
            public string Name { get; set; } = "";
        }

        public class ThingContext : DbContextBase
        {
            public ThingContext(DbContextOptions<ThingContext> options) : base(options) { }

            public DbSet<Thing> Things { get; set; } = default!;

            public Action? OnReset { get; set; }

            /// <summary>Keeps <see cref="OnReset"/> across resets, to count them.</summary>
            public bool ResetsAreCounted { get; set; }

            public void Configure(string schema, ITimeTracker tracker)
            {
                Schema = schema;
                TimeTracker = tracker;
            }

            protected override void ConfigureModel(ModelBuilder modelBuilder)
                => modelBuilder.Entity<Thing>(entity =>
                {
                    entity.ToTable("Things");
                    entity.IsAuditable();
                });

            protected override void OnResetState()
            {
                var onReset = OnReset;
                if (!ResetsAreCounted)
                {
                    OnReset = null;
                }
                onReset?.Invoke();
                base.OnResetState();
            }
        }

        public class TenantThing
        {
            public int Id { get; set; }
            public string Name { get; set; } = "";
        }

        public class TenantThingContext : MultiTenantDbContext
        {
            public TenantThingContext(DbContextOptions<TenantThingContext> options) : base(options) { }

            public override TenantMismatchMode TenantMismatchMode { get; set; } = TenantMismatchMode.Throw;
            public override TenantNotSetMode TenantNotSetMode { get; set; } = TenantNotSetMode.Overwrite;

            public DbSet<TenantThing> Things { get; set; } = default!;

            protected override void ConfigureModel(ModelBuilder modelBuilder)
                => modelBuilder.Entity<TenantThing>(entity =>
                {
                    entity.ToTable("TenantThings");
                    entity.IsMultiTenant(Juice.EF.MultiTenant.SharingType.Tenant);
                });
        }
    }

    internal static class ServiceCollectionTestExtensions
    {
        public static IServiceCollection Also(this IServiceCollection services, Action<IServiceCollection> configure)
        {
            configure(services);
            return services;
        }
    }
}
