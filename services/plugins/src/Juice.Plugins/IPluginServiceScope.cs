namespace Juice.Plugins
{
    public interface IPluginServiceScope : IDisposable
    {
        IPluginServiceProvider ServiceProvider { get; }
    }
}
