namespace Juice.Conventions.StartupDiscovery
{
    internal class StartupDiscoveryFeature
    {
        public HashSet<Type> Startups { get; set; } = new HashSet<Type>();
    }
}
