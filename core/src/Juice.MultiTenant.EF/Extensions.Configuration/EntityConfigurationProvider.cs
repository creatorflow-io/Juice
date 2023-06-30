using Juice.MultiTenant.Domain.AggregatesModel.SettingsAggregate;
using Microsoft.Extensions.Configuration;

namespace Juice.MultiTenant.EF.Extensions.Configuration
{
    internal class EntityConfigurationProvider : ConfigurationProvider
    {
        private readonly ITenantSettingsRepository _repository;

        public EntityConfigurationProvider(ITenantSettingsRepository repository)
        {
            _repository = repository;
        }


        public override void Load()
        {
            Data =
                _repository.GetAllAsync(default).ConfigureAwait(false)
                .GetAwaiter().GetResult()
                .ToDictionary<TenantSettings, string, string?>(c => c.Key, c => c.Value, StringComparer.OrdinalIgnoreCase);
        }

    }
}
