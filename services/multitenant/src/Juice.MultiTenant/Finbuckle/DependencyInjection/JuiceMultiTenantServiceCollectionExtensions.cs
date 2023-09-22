using Finbuckle.MultiTenant;
using Microsoft.Extensions.DependencyInjection;

namespace Juice.MultiTenant
{
    public static class JuiceMultiTenantServiceCollectionExtensions
    {
        public static FinbuckleMultiTenantBuilder<TenantInfo> AddMultiTenant(this IServiceCollection services, Action<MultiTenantOptions>? config = null)
            => (config != null ? services.AddMultiTenant<TenantInfo>(config)
                : services.AddMultiTenant<TenantInfo>())
                .JuiceIntegration();
    }
}
