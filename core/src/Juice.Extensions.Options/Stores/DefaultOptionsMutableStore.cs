namespace Juice.Extensions.Options.Stores
{
    internal class DefaultOptionsMutableStore : OptionsMutableJsonFileStore
    {
        protected string _file = "appsettings.json";

        public DefaultOptionsMutableStore(string file)
        {
            _file = file;
        }

        protected override Task<string> GetPhysicalPathAsync()
            => Task.FromResult(_file);
    }

    internal class DefaultOptionsMutableStore<T> : DefaultOptionsMutableStore,
        IOptionsMutableStore<T>
    {
        public DefaultOptionsMutableStore(string file) : base(file)
        {
        }

        //public async Task<T> UpdateAsync(string section, T current, Action<T> applyChanges)
        //{
        //    var physicalPath = await GetPhysicalPathAsync();

        //    var dir = Path.GetDirectoryName(physicalPath);
        //    if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        //    {
        //        Directory.CreateDirectory(dir);
        //    }
        //    if (!File.Exists(physicalPath))
        //    {
        //        using (var sw = File.CreateText(physicalPath))
        //        {
        //            sw.Write("{}");
        //        }
        //    }

        //    var jObject = JsonConvert.DeserializeObject<JObject>(await File.ReadAllTextAsync(physicalPath));

        //    var sectionObject = (jObject?.TryGetValue(section, out JToken? sectionObj) ?? false) ?
        //               (JsonConvert.DeserializeObject<T>(sectionObj.ToString()) ?? current) : current;

        //    applyChanges(sectionObject);

        //    await UpdateAsync(section, sectionObject);
        //    return sectionObject;
        //}
    }
}
