using System.Collections.ObjectModel;
using Juice.Plugins.Loader;
using Juice.Plugins.Management.Events;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Juice.Plugins.Management
{
    internal class PluginsManager : IPluginsManager
    {

        public ReadOnlyCollection<IPlugin> Plugins =>
            _plugins.ToList<IPlugin>().AsReadOnly();

        private List<PluginWrapper> _plugins = new List<PluginWrapper>();

        private string[] _pluginPaths = new string[] { };
        private ILogger _logger;

        private Action<IServiceCollection>? _configureSharedServices;

        public event EventHandler<PluginLoadEventArgs> PluginLoaded;
        public event EventHandler<PluginUnloadEventArgs> PluginUnloading;

        public PluginsManager(string[] pluginPaths, ILogger<PluginsManager> logger,
            Action<IServiceCollection>? configureSharedServices)
        {
            _pluginPaths = pluginPaths;
            _logger = logger;
            _configureSharedServices = configureSharedServices;
            LoadPlugins();
        }

        private void LoadPlugins()
        {
            foreach (var pluginPath in _pluginPaths)
            {
                var plugin = new PluginWrapper(pluginPath);
                plugin.TryLoad(_configureSharedServices);

                _plugins.Add(plugin);

                if (plugin.IsLoaded)
                {
                    _logger.LogInformation($"Loaded plugin: {pluginPath}");
                }
                else if (!string.IsNullOrEmpty(plugin.Error))
                {
                    _logger.LogError($"Failed to load plugin: {pluginPath} {plugin.Error}");
                }
                OnPluginLoaded(plugin);
            }
        }

        protected virtual void OnPluginLoaded(IPlugin plugin)
        {
            EventHandler<PluginLoadEventArgs> handler = PluginLoaded;
            handler?.Invoke(this, new PluginLoadEventArgs(plugin));
        }

        protected virtual void OnPluginUnload(IPlugin plugin)
        {
            EventHandler<PluginUnloadEventArgs> handler = PluginUnloading;
            handler?.Invoke(this, new PluginUnloadEventArgs(plugin));
        }

        public (bool Loaded, string Message) LoadPlugin(string pluginPath)
        {
            var existingPlugin = _plugins.FirstOrDefault(p => p.IsSamePath(pluginPath));
            if (existingPlugin != null)
            {
                if (!existingPlugin.IsLoaded)
                {
                    existingPlugin.TryLoad(_configureSharedServices);
                    if (existingPlugin.IsLoaded)
                    {
                        _logger.LogInformation($"Loaded plugin: {pluginPath}");
                    }
                    else if (!string.IsNullOrEmpty(existingPlugin.Error))
                    {
                        _logger.LogError($"Failed to load plugin: {pluginPath} {existingPlugin.Error}");
                    }
                    OnPluginLoaded(existingPlugin);
                }

                return (existingPlugin.IsLoaded, existingPlugin.Error ??
                    (existingPlugin.IsLoaded ? "Load succeeded" : ""));
            }
            else
            {
                var plugin = new PluginWrapper(pluginPath);
                plugin.TryLoad(_configureSharedServices);
                _plugins.Add(plugin);
                if (plugin.IsLoaded)
                {
                    _logger.LogInformation($"Loaded plugin: {pluginPath}");
                }
                else if (!string.IsNullOrEmpty(plugin.Error))
                {
                    _logger.LogError($"Failed to load plugin: {pluginPath} {plugin.Error}");
                }
                OnPluginLoaded(plugin);
                return (plugin.IsLoaded, plugin.Error ??
                    (plugin.IsEnabled ? "Load succeeded" : ""));
            }
        }
        public (bool Unloaded, string Message) UnloadPlugin(string pluginPath)
        {
            var plugin = _plugins.FirstOrDefault(p => p.IsSamePath(pluginPath));
            if (plugin != null)
            {
                OnPluginUnload(plugin);
                plugin.Dispose();
                _plugins.Remove(plugin);
                _logger.LogInformation($"Unloaded plugin: {pluginPath}");
                return (true, "Unload succeeded!");
            }
            else
            {
                return (false, $"Plugin not found: {pluginPath}");
            }
        }
    }
}

