namespace Juice.Plugins.Management.Events
{
    public class PluginLoadEventArgs : EventArgs
    {
        public IPlugin Plugin { get; private set; }

        public PluginLoadEventArgs(IPlugin plugin)
        {
            Plugin = plugin;
        }
    }
}
