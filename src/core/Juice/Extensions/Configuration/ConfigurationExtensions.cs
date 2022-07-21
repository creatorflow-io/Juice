using System.Dynamic;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;

namespace Juice.Extensions.Configuration
{
    public static class ConfigurationExtensions
    {
        /// <summary>
        /// Get scalared config from configuration section then bind to type <see cref="T"/>
        /// <para>It support binding Dictionary&lt;,&gt; or other scalared type in to your T configuration</para>
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="configuration"></param>
        /// <returns>T object</returns>
        public static T GetScalaredConfig<T>(this IConfigurationSection configuration)
        {
            ExpandoObject obj = GetExpandoObject(configuration);
            var json = JsonConvert.SerializeObject(obj);
            return JsonConvert.DeserializeObject<T>(json);
        }

        private static ExpandoObject GetExpandoObject(IConfigurationSection config, string settingName = null)
        {
            settingName = settingName ?? config.Path;
            var result = new ExpandoObject();

            // retrieve all keys from your settings
            var configs = config.AsEnumerable()
                .Where(_ => string.IsNullOrEmpty(settingName) || _.Key.StartsWith(settingName))
                ;
            foreach (var kvp in configs)
            {
                var parent = result as IDictionary<string, object>;
                var path = kvp.Key.Substring(settingName?.Length ?? 0).Split(':');

                // create or retrieve the hierarchy (keep last path item for later)
                var i = 0;
                for (i = 0; i < path.Length - 1; i++)
                {
                    if (path[i] == "") { continue; }
                    if (!parent.ContainsKey(path[i]))
                    {
                        parent.Add(path[i], new ExpandoObject());
                    }

                    parent = parent[path[i]] as IDictionary<string, object>;
                }

                if (kvp.Value == null)
                {
                    continue;
                }

                // add the value to the parent
                // note: in case of an array, key will be an integer and will be dealt with later
                var key = path[i];
                parent.Add(key, kvp.Value);
            }

            // at this stage, all arrays are seen as dictionaries with integer keys
            ReplaceWithArray(null, null, result);
            return result;
        }

        private static void ReplaceWithArray(ExpandoObject parent, string key, ExpandoObject input)
        {
            if (input == null)
            {
                return;
            }

            var dict = input as IDictionary<string, object>;
            var keys = dict.Keys.ToArray();

            // it's an array if all keys are integers
            if (keys.All(k => int.TryParse(k, out var dummy)))
            {
                var array = new object[keys.Length];
                foreach (var kvp in dict)
                {
                    array[int.Parse(kvp.Key)] = kvp.Value;
                    // Edit: If structure is nested deeper we need this next line 
                    ReplaceWithArray(input, kvp.Key, kvp.Value as ExpandoObject);
                }

                var parentDict = parent as IDictionary<string, object>;
                if (parentDict != null && key != null)
                {
                    parentDict.Remove(key);
                    parentDict.Add(key, array);
                }
            }
            else
            {
                foreach (var childKey in dict.Keys.ToList())
                {
                    ReplaceWithArray(input, childKey, dict[childKey] as ExpandoObject);
                }
            }
        }
    }
}
