using Microsoft.Extensions.Logging;

namespace Juice.Storage.Abstractions
{
    public class StorageProxy : IStorage
    {
        private IEnumerable<IStorageProvider> _providers = Array.Empty<IStorageProvider>();

        public Protocol[] Protocols => _providers.SelectMany(p => p.Protocols).ToArray();

        private ILogger _logger;
        public StorageProxy(IStorageFactory factory,
            ILoggerFactory logger)
        {
            _providers = factory.CreateProviders();
            _logger = logger.CreateLogger<StorageProxy>();
        }

        #region IDisposable Support

        protected virtual void Cleanup() { }

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
        ~StorageProxy()
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

        public async Task<Stream> ReadAsync(string filePath, CancellationToken token)
        {
            foreach (var provider in _providers)
            {
                try
                {
                    var stream = await provider.ReadAsync(filePath, token);
                    if (stream != null && stream.CanRead)
                    {
                        return stream;
                    }
                    else
                    {
                        _logger.LogWarning($"Could not read file {filePath} from {provider.GetType().Name}");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Could not read file {filePath} from {provider.GetType().Name}. {ex.Message}");
                    if (_logger.IsEnabled(LogLevel.Trace))
                    {
                        _logger.LogTrace(ex.StackTrace);
                    }
                }
            }
            throw new Exception($"Could not read file {filePath} from any endpoint");
        }
        public async Task WriteAsync(string filePath, Stream stream, long offset, TransferOptions options, CancellationToken token)
        {
            foreach (var provider in _providers)
            {
                try
                {
                    await provider.WriteAsync(filePath, stream, offset, options, token);
                    return;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Could not write to {filePath} on {provider.GetType().Name}. {ex.Message}");
                    if (_logger.IsEnabled(LogLevel.Trace))
                    {
                        _logger.LogTrace(ex.StackTrace);
                    }
                }
            }
            throw new Exception($"Could not write to {filePath} on any endpoint");
        }
        public async Task<string> CreateAsync(string filePath, CreateFileOptions options, CancellationToken token)
        {
            foreach (var provider in _providers)
            {
                try
                {
                    return await provider.CreateAsync(filePath, options, token);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Could not create file {filePath} on {provider.GetType().Name}. {ex.Message}");
                    if (_logger.IsEnabled(LogLevel.Trace))
                    {
                        _logger.LogTrace(ex.StackTrace);
                    }
                }
            }
            throw new Exception($"Could not create file {filePath} on any endpoint");
        }
        public Task<bool> ExistsAsync(string filePath, CancellationToken token)
        {
            foreach (var provider in _providers)
            {
                try
                {
                    return provider.ExistsAsync(filePath, token);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Could not check if file {filePath} exists on {provider.GetType().Name}. {ex.Message}");
                    if (_logger.IsEnabled(LogLevel.Trace))
                    {
                        _logger.LogTrace(ex.StackTrace);
                    }
                }
            }
            throw new Exception($"Could not check if file {filePath} exists on any endpoint");
        }
        public Task<long> FileSizeAsync(string filePath, CancellationToken token)
        {
            foreach (var provider in _providers)
            {
                try
                {
                    return provider.FileSizeAsync(filePath, token);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Could not get file size for {filePath} on {provider.GetType().Name}. {ex.Message}");
                    if (_logger.IsEnabled(LogLevel.Trace))
                    {
                        _logger.LogTrace(ex.StackTrace);
                    }
                }
            }
            throw new Exception($"Could not get file size for {filePath} on any endpoint");
        }
        public async Task DeleteAsync(string filePath, CancellationToken token)
        {
            foreach (var provider in _providers)
            {
                try
                {
                    await provider.DeleteAsync(filePath, token);
                    return;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Could not delete file {filePath} on {provider.GetType().Name}. {ex.Message}");
                    if (_logger.IsEnabled(LogLevel.Trace))
                    {
                        _logger.LogTrace(ex.StackTrace);
                    }
                }
            }
            throw new Exception($"Could not delete file {filePath} on any endpoint");
        }

    }
}
