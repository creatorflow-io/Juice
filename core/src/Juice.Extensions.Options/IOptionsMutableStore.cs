using Newtonsoft.Json.Linq;

namespace Juice.Extensions.Options
{
    /// <summary>
    /// Define store interface to save option provided by <see cref="IOptionsMutable{T}"/>
    /// <para>You can implement for file/database or other store</para>
    /// </summary>
	public interface IOptionsMutableStore
    {
        Task UpdateAsync(Action<JObject> applyChanges);
    }

    public interface IOptionsMutableStore<T> : IOptionsMutableStore { }
}
