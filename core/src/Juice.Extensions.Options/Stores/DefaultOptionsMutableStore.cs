namespace Juice.Extensions.Options.Stores
{
    internal class DefaultOptionsMutableStore : OptionsMutableFileStore
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
    }
}
