using Finbuckle.MultiTenant;
using Finbuckle.MultiTenant.Stores;
using Juice.Domain;
using Juice.EF;
using Juice.Extensions.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Juice.MultiTenant.EF
{
    /// <summary>
    /// Provides builder methods for Finbuckle.MultiTenant services and configuration.
    /// </summary>
    public static class JuiceEFStoreFinbuckleMultiTenantBuilderExtensions
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


        /// <summary>
        /// Configure tenant for multi-tenant microservices working with Tenant DB directly
        /// </summary>
        /// <returns></returns>
        public static FinbuckleMultiTenantBuilder<TTenantInfo> ConfigureTenantEFDirectly<TTenantInfo>(this FinbuckleMultiTenantBuilder<TTenantInfo> builder, IConfiguration configuration,
            Action<DbOptions> configureTenantDb, string environment)
            where TTenantInfo : class, IDynamic, ITenantInfo, new()
        {

            builder.WithEFStore(configuration, configureTenantDb, true);

            builder.Services.AddTenantsConfiguration()
                .AddTenantsJsonFile($"appsettings.{environment}.json")
                .AddTenantsEntityConfiguration(configuration, configureTenantDb);

            return builder;
        }

    }
}
