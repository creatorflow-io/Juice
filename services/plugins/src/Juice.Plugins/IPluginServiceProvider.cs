namespace Juice.Plugins
{
    public interface IPluginServiceProvider
    {
        /// <summary>
        /// Get all services of type T
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        IEnumerable<T> GetServices<T>();

        /// <summary>
        /// Get a service of type T
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        T? GetService<T>();

        IPluginServiceScope CreateScope();
    }

}
