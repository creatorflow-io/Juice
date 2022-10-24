using System.Net;
using System.Text.RegularExpressions;
using FluentFTP;
using Juice.Storage.Abstractions;

namespace Juice.Storage.Local
{
    public class FTPStorageProvider : StorageProviderBase
    {
        private FtpClient? _client;

        public const string FtpAddressPattern = @"^(?<protocol>ftp[s]{0,1}:\/\/)*(?<host>[\w\.]+)[:]*(?<port>[0-9]+)*(?<working>[\/][^\n]+)*$";

        private string? _workingDirectory;

        public override Protocol[] Protocols => new Protocol[] { Protocol.Ftp };

        public override IStorageProvider Configure(StorageEndpoint endpoint)
        {
            StorageEndpoint = endpoint;

            Init();

            if (!string.IsNullOrWhiteSpace(endpoint.Identity))
            {
                return this.WithCredential(new NetworkCredential(endpoint.Identity, endpoint.Password));
            }

            return this;
        }

        public override IStorageProvider WithCredential(NetworkCredential credential)
        {
            CheckEndpoint();
            base.WithCredential(credential);

            if (_client == null)
            {
                Init();
            }

            _client.Credentials = Credential;

            return this;
        }

        private void Init()
        {
            var match = new Regex(FtpAddressPattern, RegexOptions.IgnoreCase).Match(StorageEndpoint.Uri);
            if (!match.Success)
            {
                throw new ArgumentException("Ftp URI does not match. Please try this patterns: ftp://localhost, ftps://localhost:2121, locahost/working/dir...");
            }
            _client = new FtpClient(match.Groups["host"].Value);

            if (match.Groups["port"].Value != null && int.TryParse(match.Groups["port"].Value, out var port))
            {
                _client.Port = port;
            }

            _workingDirectory = match.Groups["working"].Value;
        }

        private async Task EnsureConnectedAsync(CancellationToken token)
        {
            if (_client == null)
            {
                throw new Exception("Client has not initialized. Please call Configure method first.");
            }
            if (_client.IsDisposed)
            {
                Init();
            }
            if (!_client.IsConnected)
            {
                await _client.ConnectAsync(token);

            }
            if (!await _client.DirectoryExistsAsync(_workingDirectory, token))
            {
                await _client.CreateDirectoryAsync(_workingDirectory, token);
            }
            await _client.SetWorkingDirectoryAsync(_workingDirectory, token);
        }

        public override async Task<string> CreateAsync(string filePath, CreateFileOptions options, CancellationToken token)
        {
            await EnsureConnectedAsync(token);

            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory) && !await _client.DirectoryExistsAsync(directory))
            {
                await _client.CreateDirectoryAsync(directory);
            }

            if (!await _client.FileExistsAsync(filePath, token))
            {
                await (await _client.OpenWriteAsync(filePath)).DisposeAsync();
                await _client.GetReplyAsync(token);
                return filePath;
            }
            var fileExistsBehavior = options?.FileExistsBehavior ?? FileExistsBehavior.RaiseError;
            switch (fileExistsBehavior)
            {
                case FileExistsBehavior.RaiseError:
                    throw new IOException("File is already exists.");
                case FileExistsBehavior.Replace:
                    File.Delete(filePath);

                    await (await _client.OpenWriteAsync(filePath)).DisposeAsync();
                    await _client.GetReplyAsync(token);
                    return filePath;

                case FileExistsBehavior.AscendedCopyNumber:
                    var newPath = await GetNameAscendedCopyNumberAsync(filePath, default, token);
                    await (await _client.OpenWriteAsync(newPath)).DisposeAsync();
                    await _client.GetReplyAsync(token);
                    return newPath;
                default: throw new IOException("File is already exists.");
            }
        }

        public override async Task DeleteAsync(string filePath, CancellationToken token)
        {
            await EnsureConnectedAsync(token).ConfigureAwait(false);
            if (await _client.FileExistsAsync(filePath, token))
            {
                await _client.DeleteFileAsync(filePath, token);
            }
        }

        public override async Task<bool> ExistsAsync(string filePath, CancellationToken token)
        {
            await EnsureConnectedAsync(token);
            return await _client.FileExistsAsync(filePath, token);
        }

        public override async Task<long> FileSizeAsync(string filePath, CancellationToken token)
        {
            await EnsureConnectedAsync(token);
            return await _client.GetFileSizeAsync(filePath, -1, token);
        }

        public override async Task<Stream> ReadAsync(string filePath, CancellationToken token)
        {
            await EnsureConnectedAsync(token);
            return await _client.OpenReadAsync(filePath);
        }

        public override async Task WriteAsync(string filePath, Stream stream, long offset, TransferOptions options, CancellationToken token)
        {
            await EnsureConnectedAsync(token);

            if (offset > 0)
            {
                var size = await FileSizeAsync(filePath, token);
                if (offset != size)
                {
                    throw new Exception("File cannot be resume from position");
                }

                using var ostream = await _client.OpenAppendAsync(filePath);

                await stream.CopyToAsync(ostream);

            }
            else
            {
                using var ostream = await _client.OpenWriteAsync(filePath);

                await stream.CopyToAsync(ostream);

            }

        }

        protected override void Cleanup()
        {
            if (_client != null)
            {
                if (_client.IsConnected)
                {
                    _client.Disconnect();
                }
                _client.Dispose();
            }
        }

        protected override async Task<IList<string>> FindFileVersionsAsync(string filePath, CancellationToken token)
        {
            await Task.Yield();

            var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(filePath);
            var extension = Path.GetExtension(filePath);

            var directory = Path.GetDirectoryName(filePath);

            var files = string.IsNullOrEmpty(directory) ?
                await _client.GetNameListingAsync(token)
                : await _client.GetNameListingAsync(directory, token);

            return files
                .Where(f => Path.GetFileNameWithoutExtension(f).StartsWith(fileNameWithoutExtension, StringComparison.OrdinalIgnoreCase)
                    && Path.GetExtension(f).Equals(extension, StringComparison.OrdinalIgnoreCase)).ToList();
        }
    }
}
