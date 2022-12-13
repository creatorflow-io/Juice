using Finbuckle.MultiTenant;
using Juice.Tenants;
using Microsoft.Extensions.DependencyInjection;

namespace Juice.MultiTenant.Finbuckle.DependencyInjection
{
    public static class FinbuckleMultiTenantBuilderExtensions
    {

        public static FinbuckleMultiTenantBuilder<TTenantInfo> JuiceIntegration<TTenantInfo>(this FinbuckleMultiTenantBuilder<TTenantInfo> builder)
            where TTenantInfo : class, ITenantInfo, new()
        {
            builder.Services.AddScoped<ITenant>(sp => sp.GetService<Tenant>()!);

            return builder;
        }

    }
}
