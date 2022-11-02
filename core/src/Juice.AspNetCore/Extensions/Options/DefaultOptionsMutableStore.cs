using Microsoft.AspNetCore.Hosting;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Juice.Extensions.Options
{
    internal class DefaultOptionsMutableStore : IOptionsMutableStore
    {
        protected string _file = "appsettings.json";
        private string _container;

        public DefaultOptionsMutableStore(IWebHostEnvironment env, string? file)
        {
            _container = env.ContentRootPath;
            _file = !string.IsNullOrEmpty(file)
                ? $"appsettings.{file}.{env.EnvironmentName}.json"
                : $"appsettings.{env.EnvironmentName}.json";
        }

        public async Task UpdateAsync(string? tenant, Action<JObject> applyChanges)
        {
            var physicalPath = string.IsNullOrEmpty(tenant)
                ? Path.Combine(_container, _file)
                : Path.Combine(_container, "tenants", tenant, _file);

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

    internal class DefaultOptionsMutableStore<T> : DefaultOptionsMutableStore,
        IOptionsMutableStore<T>
    {
        public DefaultOptionsMutableStore(IWebHostEnvironment env, string? file) : base(env, file)
        {
        }
    }
}
