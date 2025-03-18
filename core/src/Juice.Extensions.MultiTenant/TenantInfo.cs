using Finbuckle.MultiTenant.Abstractions;
using Juice.Models;
using Juice.MultiTenant;
using Newtonsoft.Json.Linq;

namespace Juice.Extensions.MultiTenant
{
    public class TenantInfo : DynamicModel, ITenant, ITenantInfo
    {
        public TenantInfo() { }
        public TenantInfo(
            string id,
            string? identifier,
            string name,
            JObject? properties,
            string? ownerUser,
            string? tenantClass)
        {
            Id = id;
            Name = name;
            Identifier = identifier;
            OwnerUser = ownerUser;
            TenantClass = tenantClass;
            Properties = properties ?? [];
        }

        public string? Identifier { get; set; }

        public string? OwnerUser { get; private set; }
        public string? TenantClass { get; private set; }

        public string? Id { get; set; }

        public string? Name { get; set; }
    }
}
