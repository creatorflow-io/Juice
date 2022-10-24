using Juice.Storage.Abstractions;
using Microsoft.Extensions.Options;

namespace Juice.Storage.InMemory
{
    public class InMemoryStorageFactory
    {
        private InMemoryStorageOptions _storageOptions;
        private IEnumerable<IStorageProvider> _providers;

        public InMemoryStorageFactory(IOptionsSnapshot<InMemoryStorageOptions> options, IEnumerable<IStorageProvider> providers)
        {
            _storageOptions = options.Value;
            _providers = providers;
        }
        public IStorageProvider CreateStorageProvider()
        {
            var provider = _providers.FirstOrDefault(s => s.GetType().Name == _storageOptions.StorageProvider);
            if (provider == null)
            {
                throw new Exception($"{_storageOptions.StorageProvider} not found");
            }
            if (_storageOptions.Endpoint != null)
            {
                return provider.Configure(_storageOptions.Endpoint.ToStorageEndpoint());
            }
            return provider;
        }
    }
}
