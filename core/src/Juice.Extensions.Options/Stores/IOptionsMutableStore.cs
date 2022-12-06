namespace Juice.Extensions.Options.Stores
{
    /// <summary>
    /// Define store interface to save option provided by <see cref="IOptionsMutable{T}"/>
    /// <para>You can implement for file/database or other store</para>
    /// </summary>
	public interface IOptionsMutableStore
    {
        Task UpdateAsync(string section, object options);
    }

    public interface IOptionsMutableStore<T> : IOptionsMutableStore
    {
        //Task<T> UpdateAsync(string section, T current, Action<T> applyChanges);
    }
}
