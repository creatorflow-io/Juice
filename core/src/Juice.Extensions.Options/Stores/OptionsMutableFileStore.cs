using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Juice.Extensions.Options.Stores
{
    public abstract class OptionsMutableFileStore : IOptionsMutableStore
    {
        protected abstract Task<string> GetPhysicalPathAsync();
        public virtual async Task UpdateAsync(Action<JObject> applyChanges)
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

            var jObject = JsonConvert.DeserializeObject<JObject>(await File.ReadAllTextAsync(physicalPath));

            applyChanges?.Invoke(jObject);

            await File.WriteAllTextAsync(physicalPath, JsonConvert.SerializeObject(jObject, Formatting.Indented));

        }
    }
}
