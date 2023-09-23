namespace Juice.Plugins.Management.Events
{
    public class PluginUnloadEventArgs : EventArgs
    {
        public IPlugin Plugin { get; private set; }
        public PluginUnloadEventArgs(IPlugin plugin)
        {
            Plugin = plugin;
        }
    }
}
