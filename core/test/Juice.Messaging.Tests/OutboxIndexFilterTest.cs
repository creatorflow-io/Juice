using System;
using System.Linq;
using FluentAssertions;
using Juice.Messaging.Outbox;
using Juice.Messaging.Outbox.EF;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Juice.Messaging.Tests
{
    /// <summary>The outbox's partial-index filters follow the provider and the column names (#121).</summary>
    public class OutboxIndexFilterTest
    {
        private sealed class Context(DbContextOptions options, Action<ModelBuilder>? before = null) : DbContext(options), IOutboxContext
        {
            public DbSet<OutboxEvent> Outbox => Set<OutboxEvent>();
            public DbSet<OutboxDelivery> OutboxDeliveries => Set<OutboxDelivery>();

            protected override void OnModelCreating(ModelBuilder modelBuilder)
            {
                before?.Invoke(modelBuilder);
                this.ConfigureOutbox(modelBuilder);
            }
        }

        private static string?[] Filters(Action<DbContextOptionsBuilder> provider, Action<ModelBuilder>? before = null)
        {
            var options = new DbContextOptionsBuilder();
            provider(options);
            // a context built per test: the model cache is keyed by the context type, and the tests change the model
            options.ReplaceService<Microsoft.EntityFrameworkCore.Infrastructure.IModelCacheKeyFactory, NoCache>();
            using var context = new Context(options.Options, before);
            return context.Model.FindEntityType(typeof(OutboxDelivery))!.GetIndexes()
                .OrderBy(i => i.GetDatabaseName())
                .Select(i => i.GetFilter())
                .Where(f => f != null)
                .ToArray();
        }

        private sealed class NoCache : Microsoft.EntityFrameworkCore.Infrastructure.IModelCacheKeyFactory
        {
            public object Create(DbContext context, bool designTime) => new object();
        }

        [Fact(DisplayName = "SQL Server filters use brackets")]
        public void Sql_server()
        {
            Filters(o => o.UseSqlServer("Server=none")).Should().Equal(
                "[State] = 0",
                "[State] = 1",
                "[State] = 3 AND [NextAttemptOn] IS NOT NULL");
        }

        [Fact(DisplayName = "PostgreSQL filters use double quotes")]
        public void Postgre_sql()
        {
            Filters(o => o.UseNpgsql("Host=none")).Should().Equal(
                "\"State\" = 0",
                "\"State\" = 1",
                "\"State\" = 3 AND \"NextAttemptOn\" IS NOT NULL");
        }

        [Fact(DisplayName = "Filters use the column names the context gives")]
        public void Column_names()
        {
            Filters(o => o.UseNpgsql("Host=none"), model => model.Entity<OutboxDelivery>(e =>
            {
                e.Property(x => x.State).HasColumnName("state");
                e.Property(x => x.NextAttemptOn).HasColumnName("next_attempt_on");
            })).Should().Equal(
                "\"state\" = 0",
                "\"state\" = 1",
                "\"state\" = 3 AND \"next_attempt_on\" IS NOT NULL");
        }
    }
}
