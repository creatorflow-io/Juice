using Juice.Tenants;

namespace Juice.Extensions.Options.Stores
{
    internal class TenantsOptionsMutableJsonFileStore : OptionsMutableJsonFileStore, ITenantsOptionsMutableStore
    {
        protected string _file = "appsettings.json";

        private ITenant _tenant;

        public TenantsOptionsMutableJsonFileStore(ITenant tenant, string file)
        {
            _tenant = tenant;
            _file = file;
        }

        protected override Task<string> GetPhysicalPathAsync()
            => Task.FromResult(Path.Combine("tenants", _tenant.Identifier, _file));
    }

    internal class TenantsOptionsMutableFileStore<T> : DefaultOptionsMutableStore<T>,
        ITenantsOptionsMutableStore<T>
    {
        private ITenant _tenant;

        public TenantsOptionsMutableFileStore(ITenant tenant, string file) : base(file)
        {
            _tenant = tenant;
        }

        protected new Task<string> GetPhysicalPathAsync()
            => Task.FromResult(Path.Combine("tenants", _tenant.Identifier, _file));
    }
}
