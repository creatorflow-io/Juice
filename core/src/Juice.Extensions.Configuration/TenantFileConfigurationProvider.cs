using Juice.MultiTenant;
using Microsoft.Extensions.Configuration.Json;

namespace Juice.Extensions.Configuration
{
    public class TenantFileConfigurationProvider : JsonConfigurationProvider
    {
        private readonly ITenantAccessor? _tenantAccessor;
        public TenantFileConfigurationProvider(TenantFileConfigurationSource source) : base(source)
        {
            _tenantAccessor = source.TenantAccessor;
        }
        public override void Load(Stream stream)
        {
            // Read the tenant ID
            var tenantId = _tenantAccessor?.Tenant?.Identifier;
            if (string.IsNullOrWhiteSpace(tenantId))
            {
                return;
            }

            // Generate the tenant-specific file name
            var dir = Path.GetDirectoryName(Source.Path);
            var file = Path.GetFileName(Source.Path);
            var tenantConfigFile = Path.Combine(dir ?? "", "tenants", tenantId, file!);

            // Ensure the file exists
            if (!File.Exists(tenantConfigFile))
            {
                return;
            }

            using (var fileStream = File.OpenRead(tenantConfigFile))
            {
                base.Load(fileStream);
            }
            var x = this.Data;
        }
    }
}
