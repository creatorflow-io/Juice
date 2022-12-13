using Juice.MultiTenant.Domain.AggregatesModel.SettingsAggregate;
using Microsoft.Extensions.Configuration;

namespace Juice.MultiTenant.EF.Extensions.Configuration
{
    internal class EntityConfigurationProvider : ConfigurationProvider
    {
        private readonly TenantSettingsDbContext _context;

        public EntityConfigurationProvider(TenantSettingsDbContext dbContext)
        {
            _context = dbContext;
        }


        public override void Load()
        {
            if (_context.TenantInfo != null)
            {
                Data =
                     _context.Settings.ToDictionary<TenantSettings, string, string?>(c => c.Key, c => c.Value, StringComparer.OrdinalIgnoreCase);
            }
        }

    }
}
