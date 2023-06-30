using Finbuckle.MultiTenant;
using Juice.EF;
using Juice.Extensions.Configuration;
using Juice.MultiTenant.EF.Stores;
using Juice.Services;
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
        public static FinbuckleMultiTenantBuilder<TTenantInfo> WithEFStore<TTenantInfo>(this FinbuckleMultiTenantBuilder<TTenantInfo> builder, IConfiguration configuration, Action<Juice.EF.DbOptions> configureOptions)
            where TTenantInfo : class, ITenant, ITenantInfo, new()
        {
            builder.Services.AddDefaultStringIdGenerator();
            builder.Services.AddTenantDbContext(configuration, configureOptions);
            builder.WithStore<MultiTenantEFCoreStore<TTenantInfo>>(ServiceLifetime.Scoped);
            builder.Services.AddScoped<MultiTenantEFCoreStore<TTenantInfo>>();
            return builder;
        }


        /// <summary>
        /// Configure tenant for multi-tenant microservices working with Tenant DB directly
        /// </summary>
        /// <returns></returns>
        public static FinbuckleMultiTenantBuilder<TTenantInfo> ConfigureTenantEFDirectly<TTenantInfo>(this FinbuckleMultiTenantBuilder<TTenantInfo> builder, IConfiguration configuration,
            Action<DbOptions> configureTenantDb, string environment)
            where TTenantInfo : class, ITenant, ITenantInfo, new()
        {

            builder.WithEFStore(configuration, configureTenantDb);

            builder.Services.AddTenantsConfiguration()
                .AddTenantsJsonFile($"appsettings.{environment}.json")
                .AddTenantsEntityConfiguration(configuration, configureTenantDb);

            return builder;
        }

    }
}
