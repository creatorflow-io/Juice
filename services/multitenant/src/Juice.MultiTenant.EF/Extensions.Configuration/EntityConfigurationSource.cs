using Juice.Extensions.Configuration;
using Juice.MultiTenant.Domain.AggregatesModel.SettingsAggregate;
using Microsoft.Extensions.Configuration;

namespace Juice.MultiTenant.EF.Extensions.Configuration
{
    internal class EntityConfigurationSource : IConfigurationSource, ITenantsConfigurationSource
    {
        private readonly ITenantSettingsRepository _repository;

        public EntityConfigurationSource(ITenantSettingsRepository repository)
        {
            _repository = repository;
        }

        public IConfigurationProvider Build(IConfigurationBuilder builder)
        {
            return new EntityConfigurationProvider(_repository);
        }
    }
}
