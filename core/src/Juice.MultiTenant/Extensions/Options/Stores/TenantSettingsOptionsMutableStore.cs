using Juice.Extensions.Options.Stores;
using Juice.MultiTenant.Domain.AggregatesModel.SettingsAggregate;

namespace Juice.MultiTenant.Extensions.Options.Stores
{
    internal class TenantSettingsOptionsMutableStore : ITenantsOptionsMutableStore
    {
        private readonly ITenantSettingsRepository _repository;
        public TenantSettingsOptionsMutableStore(ITenantSettingsRepository repository)
        {
            _repository = repository;
        }
        public Task UpdateAsync(string section, object? options)
            => _repository.UpdateSectionAsync(section, options);
    }
}
