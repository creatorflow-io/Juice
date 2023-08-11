using Microsoft.Extensions.DependencyInjection;

namespace Juice.Storage.Abstractions.Services
{
    internal class DefaultStorageProviderFactory : IStorageProviderFactory

    {
        private IServiceProvider _serviceProvider;

        public DefaultStorageProviderFactory(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public IStorageProvider[] CreateProviders(IEnumerable<StorageEndpoint> endpoints)
        {
            var providersToReturn = new List<IStorageProvider>();
            foreach (var endpoint in endpoints)
            {
                var providers = _serviceProvider.GetServices<IStorageProvider>();
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


