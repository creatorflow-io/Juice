namespace Juice.Plugins.Tests.PluginBase
{
    public interface ICommand : IDisposable
    {
        string Name { get; }
        string Description { get; }

        string Execute();
    }
}
