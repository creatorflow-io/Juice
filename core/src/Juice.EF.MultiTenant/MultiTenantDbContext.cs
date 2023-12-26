using Finbuckle.MultiTenant;
using Finbuckle.MultiTenant.EntityFrameworkCore;
using Juice.EF;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Juice.MultiTenant.EF
{
    public abstract class MultiTenantDbContext : DbContextBase, IMultiTenantDbContext
    {
        #region Finbuckle
        public ITenantInfo? TenantInfo { get; internal set; }
        public virtual TenantMismatchMode TenantMismatchMode { get; set; } = TenantMismatchMode.Throw;

        public virtual TenantNotSetMode TenantNotSetMode { get; set; } = TenantNotSetMode.Throw;
        #endregion

        /// <summary>
        /// Please call <c>ConfigureServices(IServiceProvider serviceProvider)</c> directly in your constructor
        /// <para>or inside <c>IDbContextFactory.CreateDbContext()</c> if you are using PooledDbContextFactory</para>
        /// <para>to init internal services</para>
        /// </summary>
        /// <param name="options"></param>
        public MultiTenantDbContext(DbContextOptions options)
            : base(options)
        {

        }

        public override void ConfigureServices(IServiceProvider serviceProvider)
        {
            base.ConfigureServices(serviceProvider);

            TenantInfo = serviceProvider.GetService<ITenantInfo>();
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.ConfigureMultiTenant();
        }

        public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default(CancellationToken))
        {
            this.EnforceMultiTenant();
            return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
        }

        public override int SaveChanges(bool acceptAllChangesOnSuccess)
        {
            this.EnforceMultiTenant();
            return base.SaveChanges(acceptAllChangesOnSuccess);
        }

    }
}
