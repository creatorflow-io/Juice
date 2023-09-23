using Juice.Plugins.Management.Events;

namespace Microsoft.Extensions.DependencyInjection
{
    public class PluginOptions
    {
        public string[] AbsolutePaths { get; set; }
        public Action<IServiceCollection, IServiceProvider>? ConfigureSharedServices { get; set; }
        public EventHandler<PluginLoadEventArgs>? PluginLoaded { get; set; }
        public EventHandler<PluginUnloadEventArgs>? PluginUnloading { get; set; }
    }
}
