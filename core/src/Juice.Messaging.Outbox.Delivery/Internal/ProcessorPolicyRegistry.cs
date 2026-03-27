using Microsoft.Extensions.DependencyInjection;

namespace Juice.Messaging.Outbox.Delivery.Internal
{
    internal sealed class ProcessorPolicyRegistry
    {
        private readonly HashSet<string> _keys = new();

        public bool Register(string key) => _keys.Add(key);
        public bool IsConfigured(string key) => _keys.Contains(key);

        internal static ProcessorPolicyRegistry GetOrCreate(IServiceCollection services)
        {
            var descriptor = services.FirstOrDefault(d => d.ServiceType == typeof(ProcessorPolicyRegistry));
            if (descriptor?.ImplementationInstance is ProcessorPolicyRegistry existing)
                return existing;
            var registry = new ProcessorPolicyRegistry();
            services.AddSingleton(registry);
            return registry;
        }
    }
}
