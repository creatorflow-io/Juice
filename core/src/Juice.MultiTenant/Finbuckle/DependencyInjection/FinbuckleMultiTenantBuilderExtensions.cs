using Finbuckle.MultiTenant;
using Juice.Extensions.Configuration;
using Juice.MultiTenant.Extensions.Configuration.DependencyInjection;
using Juice.MultiTenant.Extensions.Options.DependencyInjection;
using Juice.MultiTenant.Grpc;
using Juice.Tenants;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Juice.MultiTenant.Finbuckle.DependencyInjection
{
    public static class FinbuckleMultiTenantBuilderExtensions
    {
        public static FinbuckleMultiTenantBuilder<TTenantInfo> WithGprcStore<TTenantInfo>(this FinbuckleMultiTenantBuilder<TTenantInfo> builder, string grpcEndpoint)
           where TTenantInfo : class, ITenantInfo, new()
        {
            builder.Services.AddGrpcClient<TenantStore.TenantStoreClient>(o =>
            {
                o.Address = new Uri(grpcEndpoint);
            });
            return builder.WithStore<MultiTenantGprcStore<TTenantInfo>>(ServiceLifetime.Scoped);
        }

        public static FinbuckleMultiTenantBuilder<TTenantInfo> JuiceIntegration<TTenantInfo>(this FinbuckleMultiTenantBuilder<TTenantInfo> builder)
            where TTenantInfo : class, ITenantInfo, new()
        {
            builder.Services.AddScoped<ITenant>(sp => sp.GetService<Tenant>()!);

            return builder;
        }


        /// <summary>
        /// Configure tenant for multi-tenant microservices working with gRPC
        /// <para></para>JuiceIntegration
        /// <para></para>Grpc store
        /// <para></para>DistributedCache store
        /// <para></para>TenantConfiguration with grpc
        /// <para></para>Configure MediatR, add Integration event service (NOTE: Required an event bus)
        /// </summary>
        /// <returns></returns>
        public static FinbuckleMultiTenantBuilder<TTenantInfo> ConfigureTenantClient<TTenantInfo>(this FinbuckleMultiTenantBuilder<TTenantInfo> builder, IConfiguration configuration,
            string environment)
            where TTenantInfo : class, ITenantInfo, new()
        {
            var tenantGrpcEndpoint = configuration
                .GetSection("Finbuckle:MultiTenant:Stores:Grpc:Endpoint")
                .Get<string>();

            builder.JuiceIntegration()
                    .WithGprcStore(tenantGrpcEndpoint)
                    .WithDistributedCacheStore();

            builder.Services.AddTenantsConfiguration()
                .AddTenantsJsonFile($"appsettings.{environment}.json")
                .AddTenantsGrpcConfiguration(tenantGrpcEndpoint);

            builder.Services.AddTenantSettingsOptionsMutableGrpcStore();

            return builder;
        }

    }
}
