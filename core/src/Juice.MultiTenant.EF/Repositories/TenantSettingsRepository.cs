using Juice.MultiTenant.Domain.AggregatesModel.SettingsAggregate;
using Juice.MultiTenant.Domain.Events;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Juice.MultiTenant.EF.Repositories
{
    internal class TenantSettingsRepository : ITenantSettingsRepository
    {
        private readonly TenantSettingsDbContext _dbContext;
        private readonly IMediator _mediator;
        private readonly Tenant? _tenant;

        public TenantSettingsRepository(TenantSettingsDbContext dbContext, IMediator mediator, Tenant? tenant = null)
        {
            _dbContext = dbContext;
            _mediator = mediator;
            _tenant = tenant;
        }
        public async Task DeleteAsync(string section)
        {
            if (_tenant == null)
            {
                throw new InvalidOperationException("Tenant was not resolved");
            }
            var sectionKey = section.Trim(':');
            var subSectionKey = sectionKey + ":";
            var settings = await _dbContext.Settings.Where(s => s.Key == sectionKey || s.Key.StartsWith(subSectionKey))
                .ToListAsync();

            _dbContext.RemoveRange(settings);
            await _dbContext.SaveChangesAsync();
            var evt = new TenantSettingsChangedDomainEvent(_tenant.Id, _tenant.Identifier);
            await _mediator.Publish(evt);
        }

        public async Task UpdateSectionAsync(string section, IDictionary<string, string?> options)
        {
            if (_tenant == null)
            {
                throw new InvalidOperationException("Tenant was not resolved");
            }
            var sectionKey = section.Trim(':');

            var dict = options
                .ToDictionary(kvp => sectionKey + ":" + kvp.Key, kvp => kvp.Value);

            var currentSettings = await _dbContext.Settings
                .Where(s => s.Key.StartsWith(section)).ToListAsync();

            var removeSettings = currentSettings.Where(s => !dict.ContainsKey(s.Key) || dict[s.Key] != s.Value || dict[s.Key] == null).ToArray();

            _dbContext.RemoveRange(removeSettings);

            var newSettings = dict.Where(kvp => kvp.Value != null
                    && (!currentSettings.Select(s => s.Key).Contains(kvp.Key)
                    || currentSettings.Single(s => s.Key == kvp.Key).Value != kvp.Value))
                .Select(kvp => new TenantSettings(Guid.NewGuid(), kvp.Key, kvp.Value))
                .ToArray();

            _dbContext.AddRange(newSettings);

            await _dbContext.SaveChangesAsync();

            var evt = new TenantSettingsChangedDomainEvent(_tenant.Id, _tenant.Identifier);
            await _mediator.Publish(evt);
        }
    }
}
