using Finbuckle.MultiTenant;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Juice.MultiTenant
{
    public static class JuiceFinbuckleMultiTenantBuilderExtensions
    {

        public static FinbuckleMultiTenantBuilder<TTenantInfo> JuiceIntegration<TTenantInfo>(this FinbuckleMultiTenantBuilder<TTenantInfo> builder)
            where TTenantInfo : class, ITenant, ITenantInfo, new()
        {
            builder.Services.TryAddScoped<ITenant>(sp => sp.GetService<TTenantInfo>()!);

            return builder;
        }

    }
}
