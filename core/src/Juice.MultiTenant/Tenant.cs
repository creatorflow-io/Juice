using Finbuckle.MultiTenant;
using Juice.Domain;

namespace Juice.MultiTenant
{
    public class Tenant : DynamicEntity<string>, ITenant, ITenantInfo
    {
        public string? Identifier { get; set; }
        public string? ConnectionString { get; set; }
    }
}
