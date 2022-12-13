using Finbuckle.MultiTenant;
using Finbuckle.MultiTenant.Stores;
using Juice.Domain;
using Juice.MultiTenant.Api.Grpc.Services;
using Juice.MultiTenant.EF;
using Juice.MultiTenant.EF.Grpc.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Juice.MultiTenant.Api.DependencyInjection
{
    public static class ApplicationBuilderExtensions
    {
        /// <summary>
        /// Map tenant/ tenant settings gRPC services
        /// </summary>
        /// <param name="builder"></param>
        /// <returns></returns>
        public static IEndpointRouteBuilder MapTenantGrpcServices(this IEndpointRouteBuilder builder)
        {
            builder.MapGrpcService<TenantStoreService>();
            builder.MapGrpcService<TenantSettingsStoreService>();

            return builder;
        }

        /// <summary>
        /// WARN: be careful to use this function. It may be crashed on too many tenants.
        /// </summary>
        /// <typeparam name="TTenantInfo"></typeparam>
        /// <param name="builder"></param>
        /// <returns></returns>
        public static async Task UpdateTenantDistrubutedCacheStoreAsync<TTenantInfo>(this IHost builder)
            where TTenantInfo : class, IDynamic, ITenantInfo, new()
        {
            var context = builder.Services.GetRequiredService<TenantStoreDbContext<TTenantInfo>>();
            var cacheStore = builder.Services.GetRequiredService<DistributedCacheStore<TTenantInfo>>();

            var tenants = await context.TenantInfo.ToListAsync();

            foreach (var tenant in tenants)
            {
                await cacheStore.TryAddAsync(tenant);
            }
        }
    }
}
