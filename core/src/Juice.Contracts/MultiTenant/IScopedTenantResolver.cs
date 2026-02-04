
namespace Juice.MultiTenant
{
    public interface IScopedTenantResolver
    {
        /// <summary>
        /// Resolve tenant by id and set it to the current scope
        /// </summary>
        /// <param name="tenantId"></param>
        /// <returns></returns>
        IDisposable Resolve(string? tenantId);
    }

    public interface IScopedTenantResolver<TTenant> : IScopedTenantResolver
    {
        IDisposable Resolve(TTenant? tenant);
    }
}
