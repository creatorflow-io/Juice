using Finbuckle.MultiTenant;
using Microsoft.Extensions.DependencyInjection;

namespace Juice.MultiTenant.Grpc.DependencyInjection
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
    }
}
