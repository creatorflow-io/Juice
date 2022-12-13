using Juice.MultiTenant.Api.Grpc.Services;
using Juice.MultiTenant.EF.Grpc.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace Juice.MultiTenant.Api.DependencyInjection
{
    public static class ApplicationBuilderExtensions
    {
        public static IEndpointRouteBuilder MapTenantGrpcServices(this IEndpointRouteBuilder builder)
        {
            builder.MapGrpcService<TenantStoreService>();
            builder.MapGrpcService<TenantSettingsStoreService>();

            return builder;
        }
    }
}
