using Microsoft.Extensions.Options;

namespace Juice.Extensions.Options
{
    public interface ITenantsOptions<T> : IOptionsSnapshot<T> where T : class, new()
    {
    }
}
