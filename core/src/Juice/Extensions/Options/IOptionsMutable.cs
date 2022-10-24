using Microsoft.Extensions.Options;

namespace Juice.Extensions.Options
{
    //for same pattern with IOptions, IOptionsSnapshot, IOptionsMonitor...
    public interface IOptionsMutable<out T> : IOptionsSnapshot<T> where T : class, new()
    {
        /// <summary>
        /// Update <see cref="T"/> field by field
        /// </summary>
        /// <param name="applyChanges"></param>
        /// <returns></returns>
		Task<bool> UpdateAsync(Action<T> applyChanges);
    }
}
