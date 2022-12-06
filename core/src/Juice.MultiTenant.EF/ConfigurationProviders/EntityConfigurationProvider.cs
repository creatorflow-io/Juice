using Microsoft.Extensions.Configuration;

namespace Juice.MultiTenant.EF.ConfigurationProviders
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
                Data = _context.Settings.Any()
                    ? _context.Settings.ToDictionary<TenantSettings, string, string?>(c => c.Key, c => c.Value, StringComparer.OrdinalIgnoreCase)
                    : CreateAndSaveDefaultValues(_context);
            }
        }

        static IDictionary<string, string?> CreateAndSaveDefaultValues(
            TenantSettingsDbContext context)
        {
            var settings = new Dictionary<string, string?>(
                StringComparer.OrdinalIgnoreCase)
            {
                //["WidgetOptions:EndpointId"] = "b3da3c4c-9c4e-4411-bc4d-609e2dcc5c67"
            };

            context.Settings.AddRange(
                settings.Select(kvp => new TenantSettings(Guid.NewGuid(), kvp.Key, kvp.Value))
                        .ToArray());

            context.SaveChanges();

            return settings;
        }
    }
}
