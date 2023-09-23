namespace Juice.Plugins.Tests.PluginBase
{
    public class SharedService
    {
        public Guid Id { get; private set; }
        public SharedService()
        {
            Id = Guid.NewGuid();
        }
    }
}
