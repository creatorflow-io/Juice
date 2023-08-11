using Juice.Storage.Abstractions;
using Microsoft.Extensions.Options;

namespace Juice.Storage.InMemory
{
    internal class InMemoryStorageRepository : IStorageRepository
    {
        private readonly IOptionsSnapshot<InMemoryStorageOptions> _snapshot;
        public InMemoryStorageRepository(IOptionsSnapshot<InMemoryStorageOptions> snapshot)
        {
            _snapshot = snapshot;
        }
        public Task<bool> ExistsAsync(string identity)
            => Task.FromResult(_snapshot.Value.Storages.Any(s => s.WebBasePath == identity));
        public Task<IEnumerable<StorageEndpoint>> GetEndpointsAsync(string identity)
            => Task.FromResult(_snapshot.Value.Storages.Single(s => s.WebBasePath == identity)
                .Endpoints.Select(e => e.ToStorageEndpoint()));
    }
}
