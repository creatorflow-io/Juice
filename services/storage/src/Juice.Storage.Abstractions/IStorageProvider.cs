using System.Net;

namespace Juice.Storage.Abstractions
{
    public interface IStorageProvider : IStorage
    {
        StorageEndpoint? StorageEndpoint { get; }
        NetworkCredential? Credential { get; }

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
        IStorageProvider Configure(StorageEndpoint endpoint);

    }
}
