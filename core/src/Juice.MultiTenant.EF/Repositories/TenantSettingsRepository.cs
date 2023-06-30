using System.Text;
using Finbuckle.MultiTenant;
using Juice.MultiTenant.Domain.AggregatesModel.SettingsAggregate;
using Microsoft.EntityFrameworkCore;

namespace Juice.MultiTenant.EF.Repositories
{
    internal class TenantSettingsRepository : ITenantSettingsRepository
    {
        private readonly TenantSettingsDbContext _dbContext;
        private ITenantInfo? _tenant;

        public TenantSettingsRepository(TenantSettingsDbContext dbContext,
            ITenantInfo? tenant = null)
        {
            _dbContext = dbContext;
            _tenant = tenant;
        }

        private IQueryable<TenantSettings> TenantSettings => _dbContext.TenantSettings
            .Where(s => (_tenant != null
                && Microsoft.EntityFrameworkCore.EF.Property<string>(s, "TenantId") == _tenant!.Id)
            || (_tenant == null
                && string.IsNullOrEmpty(Microsoft.EntityFrameworkCore.EF.Property<string>(s, "TenantId"))));

        private IQueryable<TenantSettings> InheritableTenantSettings => _dbContext.TenantSettings
            .OrderByDescending(s =>
                    string.IsNullOrEmpty(Microsoft.EntityFrameworkCore.EF.Property<string>(s, "TenantId")) ? 0 : 1);

        public async Task DeleteAsync(string section)
        {
            if (string.IsNullOrWhiteSpace(section))
            {
                throw new ArgumentNullException(nameof(section));
            }
            var sectionKey = section.TrimEnd(':');
            var subSectionKey = sectionKey + ":";
            var settings = await TenantSettings
                .Where(s => s.Key == sectionKey || s.Key.StartsWith(subSectionKey))
                .ToListAsync();

            _dbContext.RemoveRange(settings);
            await _dbContext.SaveChangesAsync();
        }

        /// <inheritdoc/>
        public async Task UpdateSectionAsync(string? section, IDictionary<string, string?> options)
        {
            var sectionKey = string.IsNullOrEmpty(section) ? "" : (section.Trim(':') + ":");

            // standardazing options
            // section:abc => 123 if the key is not empty
            // OR section => 123 if the key is empty
            var dict = options
                .ToDictionary(kvp =>
                kvp.Key == "" ? sectionKey.Trim(':') : (sectionKey + kvp.Key),
                kvp => kvp.Value);

            if (dict.Any(kvp => string.IsNullOrEmpty(kvp.Key)))
            {
                throw new ArgumentException("Key cannot be empty", nameof(options));
            }

            if (!string.IsNullOrEmpty(section))
            {
                // Find settings with pattern and remove them if they are not in the new settings or the value is different
                // section:*
                // section
                var hierarchyKeys = new HashSet<string>(comparer: StringComparer.OrdinalIgnoreCase);
                foreach (var key in dict.Keys)
                {
                    var builder = new StringBuilder();
                    var parts = key.Split(':');
                    for (var i = 0; i < parts.Length - 1; i++)
                    {
                        builder.Append(parts[i]);
                        hierarchyKeys.Add(builder.ToString());
                        builder.Append(':');
                    }
                }

                var currentSettings = await TenantSettings
                    .Where(s =>
                        s.Key.StartsWith(sectionKey) // sub section
                        || hierarchyKeys.Contains(s.Key)).ToListAsync();

                var removeSettings = currentSettings.Where(s =>
                    !dict.ContainsKey(s.Key)
                    || dict[s.Key] != s.Value).ToArray();

                _dbContext.RemoveRange(removeSettings);

                var newSettings = dict.Where(kvp =>
                    (!currentSettings.Select(s => s.Key).Contains(kvp.Key)
                    || currentSettings.Single(s => s.Key == kvp.Key).Value != kvp.Value))
                .Select(kvp => new TenantSettings(Guid.NewGuid(), kvp.Key, kvp.Value))
                .ToArray();

                _dbContext.AddRange(newSettings);
            }
            else
            {
                var currentSettings = await TenantSettings
                    .ToListAsync();

                var removeSettings = currentSettings.Where(s => !dict.ContainsKey(s.Key)
                    || dict[s.Key] != s.Value).ToArray();

                _dbContext.RemoveRange(removeSettings);

                var newSettings = dict.Where(kvp =>
                    (!currentSettings.Select(s => s.Key).Contains(kvp.Key)
                    || currentSettings.Single(s => s.Key == kvp.Key).Value != kvp.Value))
                .Select(kvp => new TenantSettings(Guid.NewGuid(), kvp.Key, kvp.Value))
                .ToArray();
                _dbContext.AddRange(newSettings);
            }

            await _dbContext.SaveChangesAsync();

        }

        /// <inheritdoc/>
        public async Task<IEnumerable<TenantSettings>> GetAllAsync(CancellationToken token)
        {
            var settings = await InheritableTenantSettings
                .AsNoTracking()
                .ToListAsync(token);

            var keys = new HashSet<string>(comparer: StringComparer.OrdinalIgnoreCase);

            var models = new List<TenantSettings>();
            foreach (var setting in settings)
            {
                if (keys.Add(setting.Key))
                {
                    models.Add(setting);
                }
            }
            return models;
        }

        /// <inheritdoc/>
        public void EnforceTenant(ITenantInfo tenant)
        {
            _tenant = tenant;
            _dbContext.EnforceTenant(tenant);
        }
    }
}
