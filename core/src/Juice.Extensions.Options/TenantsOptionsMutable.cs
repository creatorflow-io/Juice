using Juice.Extensions.Configuration;
using Juice.Extensions.Options.Stores;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Juice.Extensions.Options
{
    internal class TenantsOptionsMutable<T> : ITenantsOptionsMutable<T>
        where T : class, new()
    {
        private readonly string _section;
        private readonly ITenantsConfiguration _tenantsConfiguration;
        private readonly Action<T>? _configureOptions;
        private readonly IOptionsMutableStore _store;
        private readonly ILogger _logger;
        public TenantsOptionsMutable(
            IServiceProvider serviceProvider,
            string section
            )
        {
            _store = serviceProvider.GetService<ITenantsOptionsMutableStore<T>>() ?? serviceProvider.GetRequiredService<ITenantsOptionsMutableStore>();
            _tenantsConfiguration = serviceProvider.GetRequiredService<ITenantsConfiguration>();
            _section = section;
            _logger = serviceProvider.GetRequiredService<ILogger<TenantsOptionsMutable<T>>>();
        }

        public TenantsOptionsMutable(
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

        public T Get(string name)
        {
            if (_valueUpdated)
            {
                return _updatedValue;
            }
            var options = _tenantsConfiguration
                    .GetSection(name).Get<T>();
            _configureOptions?.Invoke(options);
            return options;
        }

        private T _updatedValue = default(T);
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
