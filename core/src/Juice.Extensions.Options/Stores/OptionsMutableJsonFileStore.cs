using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Juice.Extensions.Options.Stores
{
    public abstract class OptionsMutableJsonFileStore : IOptionsMutableStore
    {
        protected abstract Task<string> GetPhysicalPathAsync();
        public virtual async Task UpdateAsync(string section, object? options)
        {
            var physicalPath = await GetPhysicalPathAsync();

            var dir = Path.GetDirectoryName(physicalPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }
            if (!File.Exists(physicalPath))
            {
                using (var sw = File.CreateText(physicalPath))
                {
                    sw.Write("{}");
                }
            }

            var jObject = JsonConvert.DeserializeObject<JObject>(await File.ReadAllTextAsync(physicalPath)) ?? new JObject();

            var keys = section.Split(':');
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

            jSection2.Merge(JObject.Parse(JsonConvert.SerializeObject(options ?? new { })), new JsonMergeSettings
            {
                MergeArrayHandling = MergeArrayHandling.Replace,
                MergeNullValueHandling = MergeNullValueHandling.Merge
            });

            await File.WriteAllTextAsync(physicalPath, JsonConvert.SerializeObject(jObject, Formatting.Indented));

        }
    }
}
