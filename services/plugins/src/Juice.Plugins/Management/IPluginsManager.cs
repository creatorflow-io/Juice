using System.Collections.ObjectModel;
using Juice.Plugins.Management.Events;

namespace Juice.Plugins.Management
{
    public interface IPluginsManager
    {
        ReadOnlyCollection<IPlugin> Plugins { get; }

        /// <summary>
        /// Event raised when a plugin is loaded. Please check the plugin state before using.
        /// </summary>
        event EventHandler<PluginLoadEventArgs> PluginLoaded;
        /// <summary>
        /// Event raised when a plugin is unloading. The plugin is still loaded at this point but will be disposed after.
        /// </summary>
        event EventHandler<PluginUnloadEventArgs> PluginUnloading;

        (bool Loaded, string Message) LoadPlugin(string pluginPath);
        (bool Unloaded, string Message) UnloadPlugin(string pluginPath);
    }
}
