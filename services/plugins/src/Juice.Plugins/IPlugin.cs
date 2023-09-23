namespace Juice.Plugins
{
    public interface IPlugin
    {
        string Name { get; }
        string? Version { get; }
        string? Author { get; }
        /// <summary>
        /// Gets a value indicating whether the plugin assembly is loaded.
        /// </summary>
        bool IsLoaded { get; }
        /// <summary>
        /// Gets a value indicating whether the plugin is enabled.
        /// </summary>
        bool IsEnabled { get; }
        /// <summary>
        /// Gets a value indicating whether the plugin is initialized by call one or more Startup.ConfigureServices().
        /// </summary>
        bool IsInitialized { get; }
        string? Error { get; }
        IServiceProvider? ServiceProvider { get; }
        Type? GetType(string typeAssemblyQualifiedName);
        bool IsOwned(Type type);
    }
}
