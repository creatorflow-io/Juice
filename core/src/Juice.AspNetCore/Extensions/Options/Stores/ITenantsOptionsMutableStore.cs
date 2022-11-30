namespace Juice.Extensions.Options.Stores
{
    public interface ITenantsOptionsMutableStore : IOptionsMutableStore
    {
    }

    public interface ITenantsOptionsMutableStore<T> : ITenantsOptionsMutableStore, IOptionsMutableStore<T>
    {

    }
}
