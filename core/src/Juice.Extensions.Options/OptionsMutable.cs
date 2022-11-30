using Juice.Extensions.Options.Stores;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

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
                await _store.UpdateAsync((jObject) =>
                {
                    var sectionObject = jObject.TryGetValue(_section, out JToken section) ?
                        JsonConvert.DeserializeObject<T>(section.ToString()) : (Value ?? new T());

                    applyChanges(sectionObject);

                    var keys = _section.Split(':');
                    JObject jSection2 = jObject;
                    foreach (var key in keys)
                    {

                        if (jSection2.TryGetValue(key, out JToken section2) && section2 is JObject jObject2)
                        {
                            jSection2 = jObject2;
                        }
                        else
                        {
                            jSection2[key] = new JObject();
                            jSection2 = jSection2[key] as JObject;
                        }
                    }

                    jSection2.Merge(JObject.Parse(JsonConvert.SerializeObject(sectionObject)), new JsonMergeSettings
                    {
                        MergeArrayHandling = MergeArrayHandling.Replace,
                        MergeNullValueHandling = MergeNullValueHandling.Merge
                    });

                    _updatedValue = sectionObject;
                });

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
