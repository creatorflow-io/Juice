using Juice.EF;
using Juice.Messaging;
using Juice.Messaging.Idempotency;
using Juice.Messaging.Idempotency.EF;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Juice.Messaging.Idempotency.Tests
{
    /// <summary>
    /// Builds an <see cref="IdempotencyContext"/> + <see cref="IIdempotencyService"/> over a real database
    /// for the EF-backed tests (guarded by <c>IgnoreOnCIFact</c>). Uses SQL Server LocalDB by default;
    /// override with the <c>IDEMPOTENCY_TEST_CS</c> / <c>IDEMPOTENCY_TEST_PROVIDER</c> environment variables.
    /// </summary>
    internal sealed class EfIdempotencyFixture : IAsyncDisposable
    {
        public ServiceProvider Services { get; }
        public string Schema { get; }

        private EfIdempotencyFixture(ServiceProvider services, string schema)
        {
            Services = services;
            Schema = schema;
        }

        public static async Task<EfIdempotencyFixture> CreateAsync(IdempotencyOptions? options = null)
        {
            var provider = Environment.GetEnvironmentVariable("IDEMPOTENCY_TEST_PROVIDER") ?? "SqlServer";
            // Unique db + schema per fixture so parallel test classes never collide over EnsureCreated/EnsureDeleted.
            var suffix = Guid.NewGuid().ToString("N").Substring(0, 8);
            var schema = "idem_" + suffix;
            var cs = Environment.GetEnvironmentVariable("IDEMPOTENCY_TEST_CS")
                ?? $"Server=(localdb)\\mssqllocaldb;Database=JuiceIdempotencyTests_{suffix};Trusted_Connection=True;MultipleActiveResultSets=true";

            var services = new ServiceCollection();
            services.AddLogging();
            services.AddMessaging(); // registers IMessageSerializer + INodeIdentity
            services.AddScoped(_ => new DbOptions<IdempotencyContext> { Schema = schema, DatabaseProvider = provider });
            services.Configure<IdempotencyOptions>(o =>
            {
                if (options is null) return;
                o.InFlightTtl = options.InFlightTtl;
                o.CompletedRetention = options.CompletedRetention;
                o.MaxKeyLength = options.MaxKeyLength;
                o.PurgeInterval = options.PurgeInterval;
            });
            services.AddDbContext<IdempotencyContext>(o =>
            {
                if (provider == "PostgreSQL")
                {
                    AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);
                    o.UseNpgsql(cs);
                }
                else
                {
                    o.UseSqlServer(cs);
                }
            });
            services.AddScoped<IIdempotencyService, IdempotencyService>();

            var sp = services.BuildServiceProvider();
            using (var scope = sp.CreateScope())
            {
                var ctx = scope.ServiceProvider.GetRequiredService<IdempotencyContext>();
                await ctx.Database.EnsureCreatedAsync();
            }
            return new EfIdempotencyFixture(sp, schema);
        }

        public async ValueTask DisposeAsync()
        {
            using (var scope = Services.CreateScope())
            {
                var ctx = scope.ServiceProvider.GetRequiredService<IdempotencyContext>();
                try { await ctx.Database.EnsureDeletedAsync(); } catch { /* best effort */ }
            }
            await Services.DisposeAsync();
        }
    }
}
