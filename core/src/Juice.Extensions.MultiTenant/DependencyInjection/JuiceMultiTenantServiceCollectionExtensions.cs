using Finbuckle.MultiTenant;
using TenantInfo = Juice.Extensions.MultiTenant.TenantInfo;

namespace Microsoft.Extensions.DependencyInjection
{
    public static class JuiceMultiTenantServiceCollectionExtensions
    {
        public static MultiTenantBuilder<TenantInfo> AddMultiTenant(this IServiceCollection services, Action<MultiTenantOptions>? config = null)
            => (config != null ? services.AddMultiTenant<TenantInfo>(config) : services.AddMultiTenant<TenantInfo>())
                .AddTenantServices();
    }
}
