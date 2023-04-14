using Microsoft.AspNetCore.Mvc.ApplicationParts;

namespace Juice.Conventions.StartupDiscovery
{
    internal class StartupDiscoveryFeatureProvider : IApplicationFeatureProvider<StartupDiscoveryFeature>
    {
        public void PopulateFeature(IEnumerable<ApplicationPart> parts, StartupDiscoveryFeature feature)
        {
            var startups = parts
                .OfType<AssemblyPart>()
                .SelectMany(p => p.Types)
                .Where(t => typeof(Modular.IModuleStartup).IsAssignableFrom(t)
                    && !t.IsInterface && !t.IsAbstract
                    );

            foreach (var startup in startups)
            {
                feature.Startups.Add(startup);
            }
        }
    }
}
