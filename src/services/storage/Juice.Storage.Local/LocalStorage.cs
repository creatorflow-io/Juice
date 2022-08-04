using System.Net;
using Juice.Storage.Abstractions;

namespace Juice.Storage.Local
{
    public class LocalStorage : StorageBase
    {
        private NetworkConnection? _connection;

        public override IStorage WithCredential(NetworkCredential credential)
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

            if (_connection == null && !string.IsNullOrWhiteSpace(StorageEndpoint?.BasePath))
            {
                _connection = new NetworkConnection(StorageEndpoint.BasePath, Credential);
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
        public override Task<long> FileSizeAsync(string filePath, CancellationToken token)
        {
            EnsureConnected();

            var fullPath = Path.Combine(StorageEndpoint.Uri, filePath);
            return Task.FromResult(new FileInfo(fullPath).Length);
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

            using var ostream = File.OpenWrite(fullPath);
            ostream.Seek(offset, SeekOrigin.Begin);
            await stream.CopyToAsync(ostream);
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
            EnsureConnected();

            await Task.Yield();
            var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(filePath);
            var extension = Path.GetExtension(filePath);

            var searchPattern = fileNameWithoutExtension + "(*";

            var directory = Path.Combine(StorageEndpoint.Uri, Path.GetDirectoryName(filePath) ?? "");

            return Directory.GetFiles(directory, searchPattern)
                .Where(f => Path.GetExtension(f).Equals(extension, StringComparison.OrdinalIgnoreCase)).ToList();
        }
    }
}
