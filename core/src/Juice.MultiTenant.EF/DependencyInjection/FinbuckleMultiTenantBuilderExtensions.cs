using Finbuckle.MultiTenant;
using Finbuckle.MultiTenant.Stores;
using Juice.Domain;
using Juice.EF;
using Juice.Extensions.Configuration;
using Juice.MultiTenant.EF.Extensions.Configuration.DependencyInjection;
using Juice.MultiTenant.Extensions.Options.DependencyInjection;
using Juice.MultiTenant.Finbuckle.DependencyInjection;
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


        /// <summary>
        /// Configure tenant for multi-tenant microservices working with Tenant DB directly
        /// <para></para>JuiceIntegration
        /// <para></para>Grpc store
        /// <para></para>DistributedCache store
        /// <para></para>TenantConfiguration with grpc
        /// <para></para>Configure MediatR, add Integration event service (NOTE: Required an event bus)
        /// </summary>
        /// <returns></returns>
        public static FinbuckleMultiTenantBuilder<TTenantInfo> ConfigureTenantEFDirectly<TTenantInfo>(this FinbuckleMultiTenantBuilder<TTenantInfo> builder, IConfiguration configuration,
            Action<DbOptions> configureTenantDb, string environment)
            where TTenantInfo : class, IDynamic, ITenantInfo, new()
        {

            builder.JuiceIntegration()
                     .WithEFStore(configuration, configureTenantDb, true);

            builder.Services.AddTenantsConfiguration()
                .AddTenantsJsonFile($"appsettings.{environment}.json")
                .AddTenantsEntityConfiguration(configuration, configureTenantDb);

            builder.Services.AddTenantSettingsOptionsMutableStore();
            return builder;
        }

    }
}
