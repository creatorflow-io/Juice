namespace Juice.Extensions.Options
{
    public interface ITenantsOptionsMutableStore : IOptionsMutableStore
    {
    }

    public interface ITenantsOptionsMutableStore<T> : ITenantsOptionsMutableStore, IOptionsMutableStore<T>
    {

    }
}
