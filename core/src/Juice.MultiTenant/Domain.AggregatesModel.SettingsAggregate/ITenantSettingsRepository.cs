using Juice.Utils;

namespace Juice.MultiTenant.Domain.AggregatesModel.SettingsAggregate
{
    public interface ITenantSettingsRepository
    {
        Task UpdateSectionAsync(string section, IDictionary<string, string?> options);
        Task DeleteAsync(string section);
    }

    public static class SettingsRepositoryExtensions
    {
        public static Task UpdateSectionAsync(this ITenantSettingsRepository repository, string section, object? options)
            => repository.UpdateSectionAsync(section, JsonConfigurationParser.Parse(options));

    }
}
