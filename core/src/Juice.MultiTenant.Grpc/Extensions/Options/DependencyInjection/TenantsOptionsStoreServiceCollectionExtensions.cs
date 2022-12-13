using Juice.Extensions.Options.Stores;
using Juice.MultiTenant.Grpc.Extensions.Options.Stores;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Juice.MultiTenant.Grpc.Extensions.Options.DependencyInjection
{
    public static class TenantsOptionsStoreServiceCollectionExtensions
    {

        /// <summary>
        /// Save tenant settings to TenantSettings grpc service
        /// </summary>
        /// <param name="services"></param>
        /// <param name="file"></param>
        /// <returns></returns>
        public static IServiceCollection AddTenantSettingsOptionsMutableGrpcStore(this IServiceCollection services)
        {
            services.TryAddScoped<ITenantsOptionsMutableStore, TenantSettingsOptionsMutableGrpcStore>();
            return services;
        }
    }
}
