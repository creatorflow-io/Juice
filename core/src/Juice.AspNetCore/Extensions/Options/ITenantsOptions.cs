using Microsoft.Extensions.Options;

namespace Juice.Extensions.Options
{
    /// <summary>
    /// Per-tenant options snapshot
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public interface ITenantsOptions<T> : IOptionsSnapshot<T> where T : class, new()
    {
    }
}
