using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Juice.Storage.Abstractions.Services
{
    internal class DefaultStorageProviderFactory : IStorageProviderFactory

    {
        private IServiceProvider _serviceProvider;
        private readonly ILogger _logger;

        public DefaultStorageProviderFactory(IServiceProvider serviceProvider, ILogger<DefaultServiceProviderFactory> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        public IStorageProvider[] CreateProviders(IEnumerable<StorageEndpoint> endpoints)
        {
            var providersToReturn = new List<IStorageProvider>();
            foreach (var endpoint in endpoints)
            {
                var providers = _serviceProvider.GetServices<IStorageProvider>();
                if (!providers.Any())
                {
                    _logger.LogWarning("No storage providers were registered");
                }
                foreach (var provider in providers.Where(p => p.Protocols.Contains(endpoint.Protocol)))
                {
                    if (provider != null)
                    {
                        provider.Configure(endpoint);
                        providersToReturn.Add(provider);

                    }
                }
            }

            return providersToReturn.ToArray();
        }
    }
}


