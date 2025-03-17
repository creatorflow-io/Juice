namespace Juice.MultiTenant
{
    /// <summary>
    /// Provides the current Tenant info.
    /// </summary>
    public interface ITenantAccessor
    {
        ITenant? Tenant
        {
            get;
        }
    }
}
