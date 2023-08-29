using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Juice.Storage.Abstractions.Services
{
    /// <inheritdoc/>
    internal class RepoStorageResolver : IStorageResolveStrategy
    {
        private readonly IStorageRepository _storageRepository;

        private readonly IServiceProvider _serviceProvider;

        public RepoStorageResolver(IStorageRepository storageRepository, IServiceProvider serviceProvider)
        {
            _storageRepository = storageRepository;
            _serviceProvider = serviceProvider;
        }
        public int Priority { get; } = 0;
        public string Identity { get; private set; } = string.Empty;

        public bool IsResolved { get; private set; }

        public IEnumerable<StorageEndpoint> Endpoints => _endpoints;

        public IStorage? Storage { get; private set; }

        private IEnumerable<StorageEndpoint> _endpoints = Enumerable.Empty<StorageEndpoint>();

        public async Task<bool> TryResolveAsync(string identity)
        {
            if (await _storageRepository.ExistsAsync(identity))
            {
                _endpoints = await _storageRepository.GetEndpointsAsync(identity);
                var loggerFactory = _serviceProvider.GetRequiredService<ILoggerFactory>();
                var factory = _serviceProvider.GetRequiredService<IStorageProviderFactory>();

                var logger = loggerFactory.CreateLogger<RepoStorageResolver>();
                if (logger.IsEnabled(LogLevel.Debug))
                {
                    logger.LogDebug("Storage endpoints for {identity} resolved: {endpoints}", identity, _endpoints.Count());
                }

                var providers = factory.CreateProviders(_endpoints);

                Storage = new StorageProxy(providers, loggerFactory);

                Identity = identity;
                IsResolved = true;
                return true;
            }
            return false;
        }

        #region IDisposable Support
        protected virtual void Cleanup()
        {
            Storage = null;
            _endpoints = Enumerable.Empty<StorageEndpoint>();
        }

        private bool disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    //  dispose managed state (managed objects).

                    try
                    {
                        Cleanup();
                    }
                    catch (NotImplementedException) { }
                }
                disposedValue = true;
            }
        }

        //  override a finalizer only if Dispose(bool disposing) above has code to free unmanaged resources.
        ~RepoStorageResolver()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(false);
        }

        // This code added to correctly implement the disposable pattern.
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        #endregion
    }
}
