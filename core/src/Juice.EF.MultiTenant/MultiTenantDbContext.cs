using Finbuckle.MultiTenant;
using Finbuckle.MultiTenant.Abstractions;
using Finbuckle.MultiTenant.EntityFrameworkCore;
using Juice.EF;
using Juice.EF.MultiTenant.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Juice.MultiTenant.EF
{
    public abstract class MultiTenantDbContext : DbContextBase, IMultiTenantDbContext
    {
        #region Finbuckle
        // TenantInfo is resolved from the accessor on every access (not captured at construction),
        // so it reflects tenants resolved after the context was created and stays correct for pooled contexts.
        // Note: a long-lived context will follow the ambient tenant if it changes between operations.
        private IMultiTenantContextAccessor? _multiTenantContextAccessor;

        public virtual ITenantInfo? TenantInfo => _multiTenantContextAccessor?.MultiTenantContext.TenantInfo;
        public abstract TenantMismatchMode TenantMismatchMode { get; set; }

        public abstract TenantNotSetMode TenantNotSetMode { get; set; }

        #endregion

        public override string? TenantId => TenantInfo?.Id;
        /// <summary>
        /// Please call <c>Create(IServiceProvider serviceProvider)</c> directly in your constructor
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

            _multiTenantContextAccessor = serviceProvider.GetService<IMultiTenantContextAccessor>();
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.ConfigureMultiTenant();
        }

        public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default(CancellationToken))
        {
            this.EnforceTenantPolicies();
            return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
        }

        public override int SaveChanges(bool acceptAllChangesOnSuccess)
        {
            this.EnforceTenantPolicies();
            return base.SaveChanges(acceptAllChangesOnSuccess);
        }

    }
}
