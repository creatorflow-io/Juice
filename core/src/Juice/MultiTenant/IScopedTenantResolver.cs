
namespace Juice.MultiTenant
{
    public interface IScopedTenantResolver
    {
        IDisposable Resolve(string? tenantId);
    }

    public interface IScopedTenantResolver<TTenant> : IScopedTenantResolver
    {
        IDisposable Resolve(TTenant? tenant);
    }
}
