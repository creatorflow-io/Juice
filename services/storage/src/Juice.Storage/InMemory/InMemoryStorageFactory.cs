using Juice.Storage.Abstractions;
using Juice.Storage.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Juice.Storage.InMemory
{
    public class InMemoryStorageFactory : IStorageFactory
    {
        private IServiceProvider _serviceProvider;
        private ILogger _logger;

        public InMemoryStorageFactory(IServiceProvider serviceProvider,
            ILogger<InMemoryStorageFactory> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        public IStorageProvider? CreateProvider(Protocol protocol, StorageEndpoint endpoint)
        {
            var providers = _serviceProvider.GetServices<IStorageProvider>();
            var provider = providers.FirstOrDefault(p => p.Protocols.Contains(protocol));
            if (provider != null)
            {
                provider.Configure(endpoint);
            }
            return provider;
        }

        public IStorageProvider[] CreateProviders()
        {
            var accessor = _serviceProvider.GetRequiredService<RequestEndpointAccessor>();
            var options = _serviceProvider
                .GetRequiredService<IOptionsSnapshot<InMemoryStorageOptions>>();

            var storage = options.Value.Storages.Where(s => s.WebBasePath == accessor.Endpoint).FirstOrDefault();

            if (storage == null)
            {
                return Array.Empty<IStorageProvider>();
            }
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug($"Found {storage.Endpoints.Count()} endpoints that matched {accessor.Endpoint}");
            }
            var providersToReturn = new List<IStorageProvider>();
            foreach (var endpoint in storage.Endpoints)
            {
                var provider = CreateProvider(endpoint.Protocol, endpoint.ToStorageEndpoint());
                if (provider != null)
                {
                    providersToReturn.Add(provider);
                }
            }

            return providersToReturn.ToArray();
        }
    }
}
