using Juice.MultiTenant;

namespace Juice.Extensions.Options.Stores
{
    internal class TenantOptionsMutableJsonFileStore : OptionsMutableJsonFileStore, IOptionsMutableStore
    {
        protected string _file = "appsettings.json";

        private ITenantAccessor _tenantAccessor;

        public TenantOptionsMutableJsonFileStore(ITenantAccessor tenant, string file)
        {
            _tenantAccessor = tenant;
            _file = file;
        }

        protected override Task<string> GetPhysicalPathAsync()
            => Task.FromResult(_tenantAccessor.Tenant!=null ? Path.Combine("tenants", _tenantAccessor.Tenant.Identifier!, _file) : _file);
    }

    internal class TenantOptionsMutableJsonFileStore<T>(ITenantAccessor tenant, string file) : TenantOptionsMutableJsonFileStore(tenant, file),
        IOptionsMutableStore<T>;
}
