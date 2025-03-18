using Finbuckle.MultiTenant.Abstractions;
using Juice.MultiTenant;

namespace Juice.Extensions.MultiTenant
{
    internal class FinbuckleTenantAccessor<TTenant>(IMultiTenantContextAccessor<TTenant> accessor) : ITenantAccessor
        where TTenant : class, ITenantInfo, ITenant, new()
    {
        public ITenant? Tenant => accessor.MultiTenantContext.TenantInfo;
    }
}
