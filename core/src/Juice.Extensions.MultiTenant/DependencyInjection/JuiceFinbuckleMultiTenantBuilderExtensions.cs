using Finbuckle.MultiTenant.Abstractions;
using Finbuckle.MultiTenant;
using Juice.MultiTenant;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Juice.Extensions.MultiTenant;

namespace Microsoft.Extensions.DependencyInjection
{
    public static class JuiceFinbuckleMultiTenantBuilderExtensions
    {

        public static MultiTenantBuilder<TTenantInfo> AddTenantAccessor<TTenantInfo>(this MultiTenantBuilder<TTenantInfo> builder)
            where TTenantInfo : class, ITenant, ITenantInfo, new()
        {
            builder.Services.TryAddScoped<ITenantAccessor, FinbuckleTenantAccessor<TTenantInfo>>();
#pragma warning disable CS8603 // Possible null reference return.
            // Will be removed in future versions, use ITenantAccessor instead.
            builder.Services.TryAddScoped<ITenant>(sp => sp.GetRequiredService<ITenantAccessor>().Tenant);
#pragma warning restore CS8603 // Possible null reference return.

            return builder;
        }

        public static MultiTenantBuilder<TTenantInfo> ConfigureAllPerTenant<TOptions, TTenantInfo>(this MultiTenantBuilder<TTenantInfo> builder, Action<TOptions, TTenantInfo> configure)
            where TTenantInfo : class, ITenant, ITenantInfo, new()
            where TOptions : class
        {
            builder.Services.ConfigureAllPerTenant(configure);

            return builder;
        }
    }
}
