using System.Net;
using System.Text.RegularExpressions;

namespace Juice.Storage.Abstractions
{
    public abstract class StorageProviderBase : IStorageProvider
    {
        protected string _copyNumberPattern = @"(?<n>[^\n]+)\((?<cn>[0-9]+)\)[\s]*\.[\S]+$";
        public NetworkCredential? Credential { get; protected set; }
        public StorageEndpoint? StorageEndpoint { get; protected set; }

        public abstract Protocol[] Protocols { get; }

        protected virtual void CheckEndpoint()
        {
            if (string.IsNullOrWhiteSpace(StorageEndpoint?.Uri))
            {
                throw new ArgumentException("StorageEndpoint uri must be configured.");
            }
        }

        public virtual IStorageProvider WithCredential(NetworkCredential credential)
        {
            Credential = credential;
            return this;
        }
        public virtual IStorageProvider Configure(StorageEndpoint endpoint)
        {
            StorageEndpoint = endpoint;
            if (!string.IsNullOrWhiteSpace(endpoint.Identity))
            {
                return this.WithCredential(new NetworkCredential(endpoint.Identity, endpoint.Password));
            }
            return this;
        }
        /// <summary>
        /// List file that has same directory, name and extension with specified file but ends with copy number
        /// Ex: abc(1).xyz
        /// </summary>
        /// <param name="filePath"></param>
        /// <returns></returns>
        protected abstract Task<IList<string>> FindFileVersionsAsync(string filePath, CancellationToken token);

        /// <summary>
        /// Match file's copy number, origin file name without extension and copy number
        /// </summary>
        /// <param name="filePath"></param>
        /// <returns></returns>
        protected virtual (int? CopyNumber, string OriginName) MatchCopyNumber(string filePath)
        {
            var fileName = Path.GetFileName(filePath);
            var nameRegex = new Regex(_copyNumberPattern, RegexOptions.IgnoreCase);
            Match match = nameRegex.Match(fileName);
            if (match.Success)
            {
                var copyNumberString = match.Groups["cn"].Value;
                if (int.TryParse(copyNumberString, out var ver))
                {
                    return (ver, match.Groups["n"].Value);
                }
                // replace original file path to search with file name without version (abc.xyz instead abc(1).xyz)
            }
            return (null, Path.GetFileNameWithoutExtension(fileName));
        }

        protected virtual async Task<string> GetNameAscendedCopyNumberAsync(string filePath, int? length, CancellationToken token)
        {
            var directory = Path.GetDirectoryName(filePath);
            directory = !string.IsNullOrWhiteSpace(directory) ? directory + Path.DirectorySeparatorChar : "";

            var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(filePath);
            var extension = Path.GetExtension(filePath);

            var searchPath = filePath;
            var copyNumber = 0;

            {
                // Check if specified file path is a copied version of an other. Ex: abc(1).xyz
                var (ver, origin) = MatchCopyNumber(filePath);
                if (ver.HasValue)
                {
                    copyNumber = ver.Value;
                    searchPath = directory + origin + extension;
                    fileNameWithoutExtension = origin;
                }
            }

            var fileVersions = await FindFileVersionsAsync(searchPath, token);

            foreach (var version in fileVersions)
            {
                // Check if specified file path is a copied version of an other. Ex: abc(1).xyz
                var (ver, origin) = MatchCopyNumber(version);
                if (ver.HasValue)
                {
                    copyNumber = ver.Value;
                    fileNameWithoutExtension = origin;
                }
            }

            copyNumber++;

            if (length.HasValue)
            {
                // calc length
                var additionNameLength = extension.Length + copyNumber.ToString().Length + 2;
                if (fileNameWithoutExtension.Length + additionNameLength > length)
                {
                    var strLen = length.Value - additionNameLength;
                    strLen = strLen > 0 ? strLen : 0;
                    fileNameWithoutExtension = fileNameWithoutExtension.Substring(0, strLen);
                }
            }
            var versionedPath = $"{directory}{fileNameWithoutExtension}({copyNumber}){extension}";

            while (await ExistsAsync(versionedPath, default))
            {
                versionedPath = $"{directory}{fileNameWithoutExtension}({++copyNumber}){extension}";
            }
            return versionedPath;
        }

        public abstract Task<string> CreateAsync(string filePath, CreateFileOptions options, CancellationToken token);
        public abstract Task<bool> ExistsAsync(string filePath, CancellationToken token);
        public abstract Task<long> FileSizeAsync(string filePath, CancellationToken token);
        public abstract Task<Stream> ReadAsync(string filePath, CancellationToken token);
        public abstract Task WriteAsync(string filePath, Stream stream, long offset, TransferOptions options, CancellationToken token);
        public abstract Task DeleteAsync(string filePath, CancellationToken token);

        #region IDisposable Support

        protected abstract void Cleanup();

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
        ~StorageProviderBase()
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
