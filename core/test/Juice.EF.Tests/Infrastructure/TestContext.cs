using System;
using Juice.EF.Extensions;
using Juice.MultiTenant.EF.Extensions;
using Juice.EF.Tests.Domain;
using Microsoft.EntityFrameworkCore;
using Juice.MultiTenant.EF;

namespace Juice.EF.Tests.Infrastructure
{

    public class TestContext : MultiTenantDbContext
    {
        public const string SCHEMA = "Contents";
        //public DbSet<Content> Contents { get; set; }

        public TestContext(IServiceProvider serviceProvider, DbContextOptions<TestContext> options) : base(options)
        {
            ConfigureServices(serviceProvider);
        }

        protected override void ConfigureModel(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Content>(entity =>
            {
                entity.ToTable(nameof(Content), SCHEMA);

                //entity.IsExpandable(this);
                //entity.IsAuditable();

                entity.Property(m => m.Code).HasMaxLength(Constants.NameLength);

                #region Indexing
                entity.HasIndex(nameof(Content.Code))
                    .IncludeProperties(m => new { m.Name })
                    .IsUnique();

                entity.HasIndex(nameof(Content.CreatedUser))
                    .HasFilter($"[{nameof(Content.CreatedUser)}] is not null")
                    .IncludeProperties(m => new { m.Name, m.Code, m.CreatedDate });
                #endregion
            });

            modelBuilder.Entity<CrossTenantContent>(entity =>
            {
                entity.ToTable(nameof(CrossTenantContent), SCHEMA);

                entity.IsCrossTenant();
            });
        }
    }
}
