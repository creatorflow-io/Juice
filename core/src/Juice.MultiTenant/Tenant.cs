using Finbuckle.MultiTenant;
using Juice.Domain;
using Juice.Tenants;

namespace Juice.MultiTenant
{
    public class Tenant : DynamicEntity<string>, ITenantInfo, ITenant
    {
        public string? Identifier { get; set; }
        public string? ConnectionString { get; set; }

        public Task TriggerConfigurationChangedAsync()
        {
            return Task.CompletedTask;
        }
    }
}
