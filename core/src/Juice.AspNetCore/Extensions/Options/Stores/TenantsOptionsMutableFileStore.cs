using Juice.Tenants;

namespace Juice.Extensions.Options.Stores
{
    internal class TenantsOptionsMutableFileStore : OptionsMutableFileStore, ITenantsOptionsMutableStore
    {
        protected string _file = "appsettings.json";

        private ITenant _tenant;

        public TenantsOptionsMutableFileStore(ITenant tenant, string file)
        {
            _tenant = tenant;
            _file = file;
        }

        protected override Task<string> GetPhysicalPathAsync()
            => Task.FromResult(Path.Combine("tenants", _tenant.Identifier, _file));
    }

    internal class TenantsOptionsMutableFileStore<T> : TenantsOptionsMutableFileStore,
        ITenantsOptionsMutableStore<T>
    {
        public TenantsOptionsMutableFileStore(ITenant tenant, string file) : base(tenant, file)
        {
        }
    }
}
