using Finbuckle.MultiTenant.Abstractions;

namespace Juice.MultiTenant.TestHelper
{
    internal class FinbuckleTenantAccessor<TTenant>(IMultiTenantContextAccessor<TTenant> accessor) : ITenantAccessor
        where TTenant : class, ITenantInfo, ITenant, new()
    {
        public ITenant? Tenant => accessor.MultiTenantContext.TenantInfo;
    }
}
