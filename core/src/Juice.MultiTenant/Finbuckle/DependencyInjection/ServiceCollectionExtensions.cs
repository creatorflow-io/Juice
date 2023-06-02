using Finbuckle.MultiTenant;
using Microsoft.Extensions.DependencyInjection;

namespace Juice.MultiTenant.Finbuckle.DependencyInjection
{
    public static class ServiceCollectionExtensions
    {
        public static FinbuckleMultiTenantBuilder<Tenant> AddMultiTenant(this IServiceCollection services, Action<MultiTenantOptions>? config = null)
            => (config != null ? services.AddMultiTenant<Tenant>(config)
                : services.AddMultiTenant<Tenant>())
                .JuiceIntegration();
    }
}
