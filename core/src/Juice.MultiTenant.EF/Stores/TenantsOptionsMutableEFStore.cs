using Juice.Extensions.Options.Stores;
using Juice.Utils;
using Microsoft.EntityFrameworkCore;

namespace Juice.MultiTenant.EF.Stores
{
    internal class TenantsOptionsMutableEFStore : ITenantsOptionsMutableStore
    {
        private readonly TenantSettingsDbContext _dbContext;
        public TenantsOptionsMutableEFStore(TenantSettingsDbContext tenantDbContext)
        {
            _dbContext = tenantDbContext;
        }
        public async Task UpdateAsync(string section, object? options)
        {
            var dict = JsonConfigurationParser.Parse(options)
                .ToDictionary(kvp => section + ":" + kvp.Key, kvp => kvp.Value);

            var currentSettings = await _dbContext.Settings
                .Where(s => s.Key.StartsWith(section)).ToListAsync();

            _dbContext.RemoveRange(currentSettings);
            var newSettings = dict.Where(kvp => kvp.Value != null)
                .Select(kvp => new TenantSettings(Guid.NewGuid(), kvp.Key, kvp.Value))
                .ToArray();
            _dbContext.AddRange(newSettings);

            await _dbContext.SaveChangesAsync();
        }
    }
}
