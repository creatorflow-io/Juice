using System;
using Juice.MultiTenant.EF.Extensions;
using Juice.EF.Tests.Domain;
using Microsoft.EntityFrameworkCore;
using Juice.MultiTenant.EF;
using Juice.EF.Extensions;
using Juice.EventBus.Transactional.EF;
using Juice.EventBus.Delivery;

namespace Juice.EF.Tests.Infrastructure
{

    public class TestContext : MultiTenantDbContext, IOutboxContext
    {
        public override string? User => "test-user";

        public override Finbuckle.MultiTenant.EntityFrameworkCore.TenantMismatchMode TenantMismatchMode { get; set; } = Finbuckle.MultiTenant.EntityFrameworkCore.TenantMismatchMode.Throw;
        public override Finbuckle.MultiTenant.EntityFrameworkCore.TenantNotSetMode TenantNotSetMode { get; set; } = Finbuckle.MultiTenant.EntityFrameworkCore.TenantNotSetMode.Overwrite;

        public DbSet<OutboxEvent> Outbox { get; set; }
        public DbSet<OutboxDelivery> OutboxDeliveries { get; set; }

        public TestContext(IServiceProvider serviceProvider, DbContextOptions<TestContext> options) : base(options)
        {
            ConfigureServices(serviceProvider);
            Schema = "Contents";
        }

        protected TestContext(IServiceProvider serviceProvider, DbContextOptions options) : base(options)
        {
            ConfigureServices(serviceProvider);
            Schema = "Contents";
        }

        protected override void ConfigureModel(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Content>(entity =>
            {
                entity.ToTable(nameof(Content), Schema);

                entity.IsExpandable(this);
                //entity.IsAuditable();

                entity.Property(m => m.Code).HasMaxLength(Constants.NameLength);

                entity.Property(m => m.AlternativeCreatedUser).HasMaxLength(Constants.NameLength);
                entity.Property(m => m.AlternativeModifiedUser).HasMaxLength(Constants.NameLength);

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
                entity.ToTable(nameof(CrossTenantContent), Schema);

                entity.IsMultiTenant(MultiTenant.SharingType.None);
            });

            new OutboxEntityTypeConfiguration(Schema).Configure(modelBuilder.Entity<OutboxEvent>());
            new OutboxDeliveryEntityTypeConfiguration(Schema).Configure(modelBuilder.Entity<OutboxDelivery>());
        }
    }

    public class TenantSharedTestContext : TestContext
    {
        public TenantSharedTestContext(IServiceProvider serviceProvider, DbContextOptions<TenantSharedTestContext> options)
            : base(serviceProvider, options)
        {
        }

        protected override void ConfigureModel(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<CrossTenantContent>(entity =>
            {
                entity.ToTable(nameof(CrossTenantContent), Schema);

                entity.IsMultiTenant(MultiTenant.SharingType.Tenant);
            });
            new OutboxEntityTypeConfiguration(Schema).Configure(modelBuilder.Entity<OutboxEvent>());
            new OutboxDeliveryEntityTypeConfiguration(Schema).Configure(modelBuilder.Entity<OutboxDelivery>());
        }
    }

    public class GlobalSharedTestContext : TestContext
    {
        public GlobalSharedTestContext(IServiceProvider serviceProvider, DbContextOptions<GlobalSharedTestContext> options)
            : base(serviceProvider, options)
        {
        }
        protected override void ConfigureModel(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<CrossTenantContent>(entity =>
            {
                entity.ToTable(nameof(CrossTenantContent), Schema);
                entity.IsMultiTenant(MultiTenant.SharingType.Global);
            });

            new OutboxEntityTypeConfiguration(Schema).Configure(modelBuilder.Entity<OutboxEvent>());
            new OutboxDeliveryEntityTypeConfiguration(Schema).Configure(modelBuilder.Entity<OutboxDelivery>());
        }
    }
}
