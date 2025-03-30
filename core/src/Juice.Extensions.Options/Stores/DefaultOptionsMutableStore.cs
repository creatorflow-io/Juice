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

    internal class DefaultOptionsMutableStore<T>(string file) : DefaultOptionsMutableStore(file),
        IOptionsMutableStore<T>;
}
