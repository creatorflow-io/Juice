using Finbuckle.MultiTenant;
using TenantInfo = Juice.Extensions.MultiTenant.TenantInfo;

namespace Microsoft.Extensions.DependencyInjection
{
    public static class JuiceMultiTenantServiceCollectionExtensions
    {
#if NET8_0_OR_GREATER
        public static MultiTenantBuilder<TenantInfo> AddMultiTenant(this IServiceCollection services, Action<MultiTenantOptions<TenantInfo>>? config = null)
            => (config != null ? services.AddMultiTenant<TenantInfo>(config) : services.AddMultiTenant<TenantInfo>())
                .AddTenantServices();
#else
        public static MultiTenantBuilder<TenantInfo> AddMultiTenant(this IServiceCollection services, Action<MultiTenantOptions>? config = null)
            => (config != null ? services.AddMultiTenant<TenantInfo>(config) : services.AddMultiTenant<TenantInfo>())
                .AddTenantServices();
#endif
    }
}
