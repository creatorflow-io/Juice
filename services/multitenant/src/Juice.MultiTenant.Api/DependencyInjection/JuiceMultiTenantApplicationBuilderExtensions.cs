using Juice.MultiTenant.Api.Grpc.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace Juice.MultiTenant.Api
{
    public static class JuiceMultiTenantApplicationBuilderExtensions
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


    }
}
