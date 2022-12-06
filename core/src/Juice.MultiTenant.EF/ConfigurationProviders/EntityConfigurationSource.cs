using Juice.Extensions.Configuration;
using Microsoft.Extensions.Configuration;

namespace Juice.MultiTenant.EF.ConfigurationProviders
{
    internal class EntityConfigurationSource : IConfigurationSource, ITenantsConfigurationSource
    {
        private readonly TenantSettingsDbContext _context;

        public EntityConfigurationSource(TenantSettingsDbContext dbContext)
        {
            _context = dbContext;
        }

        public IConfigurationProvider Build(IConfigurationBuilder builder)
        {
            return new EntityConfigurationProvider(_context);
        }
    }
}
