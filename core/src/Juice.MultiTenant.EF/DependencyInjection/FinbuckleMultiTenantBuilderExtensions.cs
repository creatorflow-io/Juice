using Finbuckle.MultiTenant;
using Finbuckle.MultiTenant.Stores;
using Juice.Domain;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Juice.MultiTenant.EF.DependencyInjection
{
    /// <summary>
    /// Provides builder methods for Finbuckle.MultiTenant services and configuration.
    /// </summary>
    public static class FinbuckleMultiTenantBuilderExtensions
    {
        /// <summary>
        /// Adds an EFCore based multitenant store to the application. Will also add the database context service unless it is already added.
        /// </summary>
        /// <returns>The same MultiTenantBuilder passed into the method.</returns>
        // ReSharper disable once InconsistentNaming
        public static FinbuckleMultiTenantBuilder<TTenantInfo> WithEFStore<TTenantInfo>(this FinbuckleMultiTenantBuilder<TTenantInfo> builder, IConfiguration configuration, Action<Juice.EF.DbOptions> configureOptions, bool migrate)
            where TTenantInfo : class, IDynamic, ITenantInfo, new()
        {
            builder.Services.AddTenantDbContext<TTenantInfo>(configuration, configureOptions, migrate);
            builder.WithStore<EFCoreStore<TenantStoreDbContext<TTenantInfo>, TTenantInfo>>(ServiceLifetime.Scoped);
            return builder;
        }
    }
}
