using System;
using Juice.MultiTenant.EF.Extensions;
using Juice.EF.Tests.Domain;
using Microsoft.EntityFrameworkCore;
using Juice.MultiTenant.EF;
using Juice.EF.Extensions;
using Microsoft.EntityFrameworkCore.Infrastructure;

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

                entity.IsExpandable(this);
                //entity.IsAuditable();

                entity.Property(m => m.Code).HasMaxLength(Constants.NameLength);

                #region Indexing
                var indexBuilder = entity.HasIndex(nameof(Content.Code))
                    .IsUnique()
                ;
                if (Database.IsSqlServer())
                {
                    SqlServerIndexBuilderExtensions.IncludeProperties(indexBuilder, m => new { m.Name });
                }
                else if (Database.IsNpgsql())
                {
                    NpgsqlIndexBuilderExtensions.IncludeProperties(indexBuilder, m => new { m.Name });
                }

                var indexBuilder1 = entity.HasIndex(nameof(Content.CreatedUser))
                ;
                if (Database.IsSqlServer())
                {
                    SqlServerIndexBuilderExtensions.IncludeProperties(indexBuilder1, m => new { m.Name, m.Code, m.CreatedDate })
                        .HasFilter($"[{nameof(Content.CreatedUser)}] is not null")
                    ;
                }
                else if (Database.IsNpgsql())
                {
                    NpgsqlIndexBuilderExtensions.IncludeProperties(indexBuilder1, m => new { m.Name, m.Code, m.CreatedDate });
                }
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
