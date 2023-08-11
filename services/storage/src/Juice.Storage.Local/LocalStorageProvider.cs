using System.Net;
using Juice.Storage.Abstractions;
using Microsoft.Extensions.Logging;
using Polly;

namespace Juice.Storage.Local
{
    public class LocalStorageProvider : StorageProviderBase
    {
        private ILogger _logger;

        private NetworkConnection? _connection;

        private readonly int _maxRetryCount = 3;

        public override Protocol[] Protocols => new Protocol[] { Protocol.Unc, Protocol.LocalDisk, Protocol.VirtualDirectory };

        public LocalStorageProvider(ILogger<LocalStorageProvider> logger)
        {
            _logger = logger;
        }

        public override IStorageProvider WithCredential(NetworkCredential credential)
        {
            base.WithCredential(credential);

            if (string.IsNullOrWhiteSpace(StorageEndpoint?.BasePath))
            {
                throw new InvalidOperationException("StorageEndpoint base path must be configured before use the network credential.");
            }
            if (_connection != null)
            {
                _connection.Dispose();
                _connection = null;
            }
            return this;
        }

        private void EnsureConnected()
        {
            CheckEndpoint();

            if (_connection == null
                && !string.IsNullOrWhiteSpace(StorageEndpoint?.BasePath)
                && !string.IsNullOrEmpty(Credential?.UserName))
            {
                var policy = Policy.Handle<Exception>()
                .WaitAndRetry(2, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)), (ex, time) =>
                {
                    _logger.LogError(ex, "[Policy] Retrying init network connection to {Path} after {Timeout}s ({ExceptionMessage})", StorageEndpoint?.BasePath ?? "", $"{time.TotalSeconds:n1}", ex.Message);
                });

                policy.Execute(() =>
                {
                    _connection = new NetworkConnection(StorageEndpoint.BasePath, Credential);
                });
            }
        }

        public override async Task<string> CreateAsync(string filePath, CreateFileOptions options, CancellationToken token)
        {
            EnsureConnected();

            if (string.IsNullOrWhiteSpace(filePath))
            {
                throw new ArgumentNullException(nameof(filePath));
            }
            var fullPath = Path.Combine(StorageEndpoint.Uri, filePath);
            var directory = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            if (!await ExistsAsync(fullPath, token))
            {
                await File.Create(fullPath).DisposeAsync();
                return filePath;
            }
            var fileExistsBehavior = options?.FileExistsBehavior ?? FileExistsBehavior.RaiseError;
            switch (fileExistsBehavior)
            {
                case FileExistsBehavior.RaiseError:
                    throw new IOException("File is already exists.");
                case FileExistsBehavior.Replace:
                    File.Delete(fullPath);

                    await File.Create(fullPath).DisposeAsync();
                    return filePath;

                case FileExistsBehavior.AscendedCopyNumber:
                    var newPath = await GetNameAscendedCopyNumberAsync(filePath, default, token);
                    fullPath = Path.Combine(StorageEndpoint.Uri, newPath);
                    await File.Create(fullPath).DisposeAsync();
                    return newPath;
                default: throw new IOException("File is already exists.");
            }

        }
        public override Task DeleteAsync(string filePath, CancellationToken token)
        {
            EnsureConnected();

            var fullPath = Path.Combine(StorageEndpoint.Uri, filePath);
            File.Delete(fullPath);
            return Task.CompletedTask;
        }
        public override Task<bool> ExistsAsync(string filePath, CancellationToken token)
        {
            EnsureConnected();

            var fullPath = Path.Combine(StorageEndpoint.Uri, filePath);
            return Task.FromResult(File.Exists(fullPath));
        }
        public override async Task<long> FileSizeAsync(string filePath, CancellationToken token)
        {
            EnsureConnected();

            var policy = Policy.Handle<IOException>()
                .WaitAndRetry(_maxRetryCount, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)), (ex, time) =>
                {
                    _logger.LogError(ex, "[Policy] Retrying get file size: {filePath} after {Timeout}s ({ExceptionMessage})", filePath, $"{time.TotalSeconds:n1}", ex.Message);
                });

            return policy.Execute(() =>
            {
                var fullPath = Path.Combine(StorageEndpoint.Uri, filePath);
                return new FileInfo(fullPath).Length;
            });
        }

        public override Task<Stream> ReadAsync(string filePath, CancellationToken token)
        {
            EnsureConnected();

            var fullPath = Path.Combine(StorageEndpoint.Uri, filePath);
            return Task.FromResult<Stream>(File.OpenRead(fullPath));
        }

        public override async Task WriteAsync(string filePath, Stream stream, long offset, TransferOptions options, CancellationToken token)
        {
            EnsureConnected();

            var fullPath = Path.Combine(StorageEndpoint.Uri, filePath);

            if (offset > 0)
            {
                var size = await FileSizeAsync(filePath, token);
                if (offset != size)
                {
                    throw new Exception("File cannot be resume from position");
                }
            }

            var policy = Policy.Handle<IOException>()
                .WaitAndRetryAsync(_maxRetryCount, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                (ex, time) =>
                {
                    _logger.LogError(ex, "[Policy] Retrying write file: {filePath} after {Timeout}s ({ExceptionMessage})", filePath, $"{time.TotalSeconds:n1}", ex.Message);
                });
            await policy.ExecuteAsync(async () =>
            {
                using var ostream = File.OpenWrite(fullPath);
                ostream.Seek(0L, SeekOrigin.End);
                try
                {
                    if (options.BufferSize.HasValue)
                    {
                        await stream.CopyToAsync(ostream, options.BufferSize.Value, token);
                    }
                    else
                    {
                        await stream.CopyToAsync(ostream, token);
                    }
                }
                catch (IOException)
                {
                    if (!token.IsCancellationRequested)
                    {
                        throw;
                    }
                    else
                    {
                        throw new OperationCanceledException(token);
                    }
                }
                finally
                {
                    try
                    {
                        await ostream.FlushAsync();
                        ostream.Close();
                    }
                    catch (Exception)
                    {
                        // ignored
                    }
                }
            });
        }

        protected override void Cleanup()
        {
            if (_connection != null)
            {
                _connection.Dispose();
            }
        }

        protected override async Task<IList<string>> FindFileVersionsAsync(string filePath, CancellationToken token)
        {
            await Task.Yield();
            var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(filePath);
            var extension = Path.GetExtension(filePath);

            var searchPattern = fileNameWithoutExtension;

            var directory = Path.Combine(StorageEndpoint.Uri, Path.GetDirectoryName(filePath) ?? "");

            return Directory.GetFiles(directory, searchPattern)
                .Where(f => Path.GetExtension(f).Equals(extension, StringComparison.OrdinalIgnoreCase)).ToList();
        }
    }
}
