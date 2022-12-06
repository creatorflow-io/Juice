using Juice.Extensions.Options.Stores;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Juice.Extensions.Options
{
    /// <summary>
    /// Implementation for <see cref="IOptionsMutable{T}"/>, use registerd <see cref="IOptionsMutableStore"/> to save change
    /// </summary>
    /// <typeparam name="T"></typeparam>
	internal class OptionsMutable<T> : IOptionsMutable<T> where T : class, new()
    {
        private readonly IOptionsMonitor<T> _options;
        private readonly string _section;
        private readonly Action<T> _configureOptions;
        private readonly IOptionsMutableStore _store;

        public OptionsMutable(
            IServiceProvider provider,
            string section
            )
        {
            _store = provider.GetService<IOptionsMutableStore<T>>() ?? provider.GetRequiredService<IOptionsMutableStore>();
            _options = provider.GetRequiredService<IOptionsMonitor<T>>();
            _section = section;
        }

        public OptionsMutable(
            IServiceProvider provider,
            string section,
            Action<T> configureOptions) : this(provider, section)
        {
            _configureOptions = configureOptions;
        }

        private T _updatedValue = default(T);
        private bool _valueUpdated = false;
        public T Value
        {
            get
            {
                if (_valueUpdated)
                {
                    return _updatedValue;
                }
                var options = _options.CurrentValue;
                _configureOptions?.Invoke(options);
                return options;
            }
        }
        public T Get(string name) => _options.Get(name);

        public async Task<bool> UpdateAsync(Action<T> applyChanges)
        {
            try
            {
                //if (_store is IOptionsMutableStore<T> storeT)
                //{
                //    _updatedValue = await storeT.UpdateAsync(_section, Value ?? new T(), applyChanges);
                //}
                //else
                {
                    var sectionObject = Value;
                    applyChanges(sectionObject);
                    await _store.UpdateAsync(_section, sectionObject);
                    _updatedValue = sectionObject;
                }

                _valueUpdated = true;

                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}
