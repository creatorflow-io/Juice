namespace Juice.Extensions.Options
{
    public interface ITenantsOptionsMutable<T> : ITenantsOptions<T>, IOptionsMutable<T>
        where T : class, new()
    {
    }
}
