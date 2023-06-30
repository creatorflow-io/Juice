using Finbuckle.MultiTenant;
using Juice.Utils;

namespace Juice.MultiTenant.Domain.AggregatesModel.SettingsAggregate
{
    public interface ITenantSettingsRepository
    {
        /// <summary>
        /// Update configuration section for tenant, if section is null, update root section.
        /// <para>All tenant settings in section will be removed if its key does not exist in the new options</para>
        /// </summary>
        /// <param name="section"></param>
        /// <param name="options"></param>
        /// <param name="inheritedKey"></param>
        /// <returns></returns>
        Task UpdateSectionAsync(string? section, IDictionary<string, string?> options);
        Task DeleteAsync(string section);

        /// <summary>
        /// Get all settings for the current tenant or inherited from the root tenant, priority is given to tenant-specific settings
        /// </summary>
        /// <param name="token"></param>
        /// <returns></returns>
        Task<IEnumerable<TenantSettings>> GetAllAsync(CancellationToken token);

        /// <summary>
        /// Enforce tenant for the repository, if tenant is null, use root tenant
        /// </summary>
        /// <param name="tenant"></param>
        void EnforceTenant(ITenantInfo tenant);
    }

    public static class SettingsRepositoryExtensions
    {
        /// <summary>
        /// Update configuration section for tenant, if section is null, update root section
        /// </summary>
        /// <param name="section"></param>
        /// <param name="options"></param>
        /// <returns></returns>
        public static Task UpdateSectionAsync(this ITenantSettingsRepository repository, string? section, object? options)
            => repository.UpdateSectionAsync(section, JsonConfigurationParser.Parse(options));

    }
}
