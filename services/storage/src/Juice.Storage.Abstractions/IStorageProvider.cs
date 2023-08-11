using System.Net;

namespace Juice.Storage.Abstractions
{
    /// <summary>
    /// Implementation of this interface should be able to connect to storage for read/write file operations by specified protocol
    /// </summary>
    public interface IStorageProvider : IStorage
    {
        StorageEndpoint? StorageEndpoint { get; }
        NetworkCredential? Credential { get; }
        int Priority { get; }

        /// <summary>
        /// Configure credential to authenticate
        /// </summary>
        /// <param name="credential"></param>
        /// <returns></returns>
        IStorageProvider WithCredential(NetworkCredential credential);

        /// <summary>
        /// Configure endpoint info and init connection if needed
        /// </summary>
        /// <param name="endpoint"></param>
        /// <returns></returns>
        IStorageProvider Configure(StorageEndpoint endpoint, int? priority = default);

    }
}
