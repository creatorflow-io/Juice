
namespace Juice.MultiTenant
{
    public interface IScopedTenantResolver
    {
        IDisposable Resolve(string? tenantId);
    }
}
