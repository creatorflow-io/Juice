using Juice.Extensions.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Juice.Extensions.Options
{
    internal class TenantsOptionsMutable<T> : ITenantsOptionsMutable<T>
        where T : class, new()
    {
        private readonly string _section;
        private readonly ITenantsConfiguration _tenantsConfiguration;
        private readonly Action<T>? _configureOptions;
        private readonly IOptionsMutableStore _store;
        public TenantsOptionsMutable(
            IServiceProvider serviceProvider,
            string section
            )
        {
            _store = serviceProvider.GetService<ITenantsOptionsMutableStore<T>>() ?? serviceProvider.GetRequiredService<ITenantsOptionsMutableStore>();
            _tenantsConfiguration = serviceProvider.GetRequiredService<ITenantsConfiguration>();
            _section = section;
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
