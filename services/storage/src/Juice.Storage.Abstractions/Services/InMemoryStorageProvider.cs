namespace Juice.Storage.Abstractions.Services
{
    /// <summary>
    /// For testing purpose
    /// </summary>
    internal class InMemoryStorageProvider : StorageProviderBase
    {
        public override int Priority => 99;
        private readonly Dictionary<string, byte[]> _storage = new Dictionary<string, byte[]>();
        public override Protocol[] Protocols => new Protocol[] { Protocol.Smb, Protocol.LocalDisk, Protocol.VirtualDirectory, Protocol.Ftp };

        public override Task<string> CreateAsync(string filePath, CreateFileOptions options, CancellationToken token)
        {
            _storage.Add(filePath, new byte[0]);
            return Task.FromResult(filePath);
        }

        public override Task<bool> DeleteAsync(string filePath, CancellationToken token = default)
        {
            _storage.Remove(filePath);
            return Task.FromResult(true);
        }
        public override Task<bool> ExistsAsync(string filePath, CancellationToken token = default)
        {
            return Task.FromResult(_storage.ContainsKey(filePath));
        }

        public override Task<long> FileSizeAsync(string filePath, CancellationToken token)
        {
            if (!_storage.ContainsKey(filePath))
            {
                throw new FileNotFoundException();
            }
            return Task.FromResult((long)_storage[filePath].Length);
        }

        public override Task<Stream> ReadAsync(string filePath, CancellationToken token = default)
        {
            if (!_storage.ContainsKey(filePath))
            {
                throw new FileNotFoundException();
            }
            return Task.FromResult((Stream)new MemoryStream(_storage[filePath]));
        }

        public override async Task WriteAsync(string filePath, Stream stream, long offset, TransferOptions options, CancellationToken token = default)
        {
            using var ostream = new MemoryStream();
            await ostream.WriteAsync(_storage[filePath], 0, _storage[filePath].Length);
            await stream.CopyToAsync(ostream);
            _storage[filePath] = ostream.ToArray();
        }

        protected override async Task<IList<string>> FindFileVersionsAsync(string filePath, CancellationToken token)
        {
            var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(filePath);
            var extension = Path.GetExtension(filePath);

            var directory = Path.GetDirectoryName(filePath);

            var files = _storage.Keys;

            return files
                .Where(f => Path.GetFileNameWithoutExtension(f).StartsWith(fileNameWithoutExtension, StringComparison.OrdinalIgnoreCase)
                    && Path.GetExtension(f).Equals(extension, StringComparison.OrdinalIgnoreCase)).ToList();
        }
    }
}
