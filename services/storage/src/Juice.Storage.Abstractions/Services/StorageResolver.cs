using Microsoft.Extensions.Logging;

namespace Juice.Storage.Abstractions.Services
{
    internal class StorageResolver : IStorageResolver
    {
        public string? Identity { get; private set; }

        public bool IsResolved { get; private set; }

        public IEnumerable<StorageEndpoint> Endpoints => _endpoints;
        private IEnumerable<StorageEndpoint> _endpoints = Enumerable.Empty<StorageEndpoint>();

        public IStorage? Storage { get; private set; }

        private IEnumerable<IStorageResolveStrategy> _resolvers;
        private ILogger _logger;

        public StorageResolver(IEnumerable<IStorageResolveStrategy> resolvers,
            ILogger<StorageResolver> logger)
        {
            _resolvers = resolvers;
            _logger = logger;
        }

        public async Task<bool> TryResolveAsync(string identity)
        {
            foreach (var resolver in _resolvers.OrderByDescending(r => r.Priority))
            {
                try
                {
                    if (await resolver.TryResolveAsync(identity))
                    {
                        _endpoints = resolver.Endpoints;
                        Storage = resolver.Storage;
                        Identity = resolver.Identity;
                        IsResolved = resolver.IsResolved;
                        return true;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error resolving storage for {identity}. Resolver: {resolver}", identity, resolver.GetType().Name);
                }
            }
            return false;
        }

        #region Disposable pattern

        protected virtual void Cleanup()
        {
            foreach (var resolver in _resolvers)
            {
                try
                {
                    resolver.Dispose();
                }
                catch (Exception ex)
                {
                    if (_logger.IsEnabled(LogLevel.Debug))
                    {
                        _logger.LogDebug(ex, "Error disposing provider {provider}", resolver.GetType().FullName);
                    }
                }
            }
            if (Storage != null)
            {
                try
                {
                    Storage.Dispose();
                }
                catch (Exception ex)
                {
                    if (_logger.IsEnabled(LogLevel.Debug))
                    {
                        _logger.LogDebug(ex, "Error disposing storage {storage}", Storage.GetType().FullName);
                    }
                }
            }
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
        ~StorageResolver()
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
