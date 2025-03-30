using Juice.Extensions.Configuration;
using Juice.Extensions.Options.Stores;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Juice.Extensions.Options
{
    internal class TenantOptionsMutable<T> : IOptionsMutable<T>
        where T : class, new()
    {
        private readonly string _section;
        private readonly ITenantConfiguration _tenantConfiguration;
        private readonly Action<T>? _configureOptions;
        private readonly IOptionsMutableStore _store;
        private readonly ILogger _logger;
        public TenantOptionsMutable(
            IServiceProvider serviceProvider,
            string section
            )
        {
            _store = serviceProvider.GetService<IOptionsMutableStore<T>>() ?? serviceProvider.GetRequiredService<IOptionsMutableStore>();
            _tenantConfiguration = serviceProvider.GetRequiredService<ITenantConfiguration>();
            _section = section;
            _logger = serviceProvider.GetRequiredService<ILogger<TenantOptionsMutable<T>>>();
        }

        public TenantOptionsMutable(
            IServiceProvider serviceProvider,
            string section,
            Action<T>? configureOptions) : this(serviceProvider, section)
        {
            _configureOptions = configureOptions;
        }

        public T Value
        {
            get
            {
                return Get(_section);
            }
        }

        public T Get(string? name)
        {
            if (name == null)
            {
                return Value;
            }
            if (_valueUpdated)
            {
                return _updatedValue;
            }
            var options = _tenantConfiguration
                    .GetSection(name).Get<T>() ?? new();
            _configureOptions?.Invoke(options);
            return options;
        }

        private T _updatedValue = new();
        private bool _valueUpdated = false;
        public async Task<bool> UpdateAsync(Action<T> applyChanges)
        {
            try
            {
                var sectionObject = Value;
                applyChanges(sectionObject);
                await _store.UpdateAsync(_section, sectionObject);
                _updatedValue = sectionObject;

                _valueUpdated = true;

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.Message, ex);
                return false;
            }
        }
    }
}
