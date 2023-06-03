using Finbuckle.MultiTenant;
using Juice.Extensions.Configuration;
using Juice.MultiTenant.Grpc.Finbuckle;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Juice.MultiTenant.Grpc
{
    public static class GrpcStoreFinbuckleMultiTenantBuilderExtensions
    {

        /// <summary>
        /// Use grpc client to resolve tenant info
        /// </summary>
        /// <typeparam name="TTenantInfo"></typeparam>
        /// <param name="builder"></param>
        /// <param name="grpcEndpoint"></param>
        /// <returns></returns>
        public static FinbuckleMultiTenantBuilder<TTenantInfo> WithGprcStore<TTenantInfo>(this FinbuckleMultiTenantBuilder<TTenantInfo> builder, string grpcEndpoint)
           where TTenantInfo : class, ITenantInfo, new()
        {
            builder.Services.AddGrpcClient<TenantStore.TenantStoreClient>(o =>
            {
                o.Address = new Uri(grpcEndpoint);
            });
            return builder.WithStore<MultiTenantGprcStore<TTenantInfo>>(ServiceLifetime.Scoped);
        }

        /// <summary>
        /// Configure tenant for multi-tenant microservices working with gRPC
        /// <para></para>JuiceIntegration
        /// <para></para>Use Grpc store and fallback to DistributedCache store after 500 milliseconds
        /// <para></para>TenantConfiguration with grpc
        /// <para></para>Configure MediatR, add Integration event service (NOTE: Required an event bus)
        /// </summary>
        /// <returns></returns>
        public static FinbuckleMultiTenantBuilder<TTenantInfo> ConfigureTenantClient<TTenantInfo>(this FinbuckleMultiTenantBuilder<TTenantInfo> builder, IConfiguration configuration,
            string environment)
            where TTenantInfo : class, ITenant, ITenantInfo, new()
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
