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
            JObject? properties = default,
            string? ownerUser = default,
            string? tier = default,
            string? region = default)
        {
            Id = id;
            Name = name;
            Identifier = identifier;
            OwnerUser = ownerUser;
            Tier = tier;
            Properties = properties ?? [];
            Region = region;
        }

        public string? Identifier { get; set; }

        public string? OwnerUser { get; private set; }
        public string? Tier { get; private set; }
        public string? Region { get; private set; }

        public string? Id { get; set; }

        public string? Name { get; set; }
    }
}
