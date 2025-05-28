using Juice.MultiTenant;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.Json;

namespace Juice.Extensions.Configuration
{
    public class TenantFileConfigurationSource : JsonConfigurationSource
    {
        /// <summary>
        /// The tenant accessor
        /// </summary>
        public ITenantAccessor? TenantAccessor { get; set; }
        public override IConfigurationProvider Build(IConfigurationBuilder builder)
        {
            EnsureDefaults(builder);
            return new TenantFileConfigurationProvider(this);
        }
    }
}
